using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Configuration;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Catalog.Infrastructure.Services.External.Grpc;
using Catalog.Infrastructure.Services.External.Grpc.Interceptors;
using Catalog.Infrastructure.Services.Internal;
using Catalog.Infrastructure.SyncDataServices.Grpc;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using User.Auth;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            string environment)
        {
            // 1. Configuración centralizada
            var catalogConfig = EnvironmentConfigurationProvider.GetConfiguration(configuration, environment);
            services.AddSingleton(catalogConfig);

            // 2. Configuración de la base de datos
            ConfigureDatabase(services, catalogConfig, environment);

            // 3. Políticas de resiliencia
            services.AddResiliencePolicies();

            // 4. Registro de repositorios
            RegisterRepositories(services);


            services.AddCatalogScopedServices();

            // 5. Servicios externos
            ConfigureExternalServices(services, configuration, environment);

            return services;
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            CatalogConfiguration catalogConfig,
            string environment)
        {
            services.AddDbContext<CatalogDbContext>((provider, options) =>
            {
                var logger = provider.GetRequiredService<ILogger<CatalogDbContext>>();

                options.UseSqlServer(catalogConfig.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: catalogConfig.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(catalogConfig.Database.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                });

                if (environment == "Development")
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                    logger.LogDebug("🔍 Habilitados logs detallados de EF Core");
                }
            });
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork>(provider =>
                new UnitOfWork(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>(),
                    provider.GetRequiredService<ICategoryRepository>(),
                    provider.GetRequiredService<IProductRepository>(),
                    provider.GetRequiredService<IProductImageRepository>()));

            services.AddScoped<ICategoryRepository>(provider =>
                new CategoryRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped<IProductRepository>(provider =>
                new ProductRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped<IProductImageRepository>(provider =>
                new ProductImageRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
        }

        private static void ConfigureExternalServices(IServiceCollection services, IConfiguration configuration, string environment)
        {
            // gRPC Services (Cliente + Servidor)
            services.AddCatalogGrpcServices(configuration);

            // RabbitMQ Services
            services.AddRabbitMQServices(configuration, environment);

            // HTTP Clients (si los tienes)
            services.AddExternalHttpClients(configuration);
        }
    }
    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddCatalogScopedServices(this IServiceCollection services)
        {
            return services
                // Servicios para Grafana/Prometheus
                .AddSingleton<IMetricsService, CatalogMetricsService>()
                .AddHostedService<MetricsInitializationService>();

             
        }
    }

    // Extensiones para servicios específicos
    public static class ExternalServicesExtensions
    {
        public static IServiceCollection AddCatalogGrpcServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Configuración de Cliente gRPC (hacia User Service)
            ConfigureGrpcClients(services, configuration);

            // 2. Configuración de Servidor gRPC (Catalog Service)
            ConfigureGrpcServer(services, configuration);

            return services;
        }

        private static void ConfigureGrpcClients(IServiceCollection services, IConfiguration configuration)
        {
            // Obtener configuración para el cliente gRPC
            var microservicesConfig = configuration.GetSection("Microservices:User");
            var serviceParams = configuration.GetSection("ServiceParameters");

            var grpcTemplate = microservicesConfig["GrpcTemplate"] ?? "http://{host}:{port}";
            var grpcHost = serviceParams["host"] ?? "localhost";
            var servicePort = serviceParams["port"] ?? "5001";

            var userGrpcUrl = grpcTemplate
                .Replace("{host}", grpcHost)
                .Replace("{port}", servicePort);

            // Cliente gRPC para User Service (AuthService)
            services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
            {
                options.Address = new Uri(userGrpcUrl);
            })
            .ConfigureChannel(channelOptions =>
            {
                channelOptions.HttpHandler = new SocketsHttpHandler
                {
                    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    EnableMultipleHttp2Connections = true
                };
            })
            .AddPolicyHandler(Policy<HttpResponseMessage>
                .Handle<RpcException>(e => e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            // Wrapper del cliente gRPC
            services.AddSingleton<IUserGrpcClient, UserGrpcClient>();
        }

        private static void ConfigureGrpcServer(IServiceCollection services, IConfiguration configuration)
        {
            // Configuración base de gRPC Server
            services.AddGrpc(options =>
            {
                // Opciones recomendadas para producción
                options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors", false);
                options.MaxReceiveMessageSize = configuration.GetValue<int>("Grpc:MaxMessageSizeMB", 4) * 1024 * 1024;
                options.IgnoreUnknownServices = true;

                // Interceptores
                options.Interceptors.Add<ExceptionInterceptor>();
            });

            // Registro del servicio gRPC específico
            services.AddScoped<CatalogGrpcService>();

            // Interceptores
            services.AddSingleton<ExceptionInterceptor>();

            // Configuración avanzada
            services.Configure<GrpcServiceOptions>(options =>
            {
                options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors");
                options.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            });
        }

        public static IServiceCollection AddRabbitMQServices(this IServiceCollection services, IConfiguration configuration, string environment)
        {
            // Configuración de RabbitMQ usando el mismo patrón que Cart
            services.AddRabbitMQMessaging(configuration, environment);

            // Consumers específicos de Catalog si los tienes
            // services.AddScoped<CategoryChangedConsumer>();

            // Background Service si lo necesitas
            // services.AddHostedService<RabbitMQConsumerHostedService>();

            return services;
        }

        public static IServiceCollection AddExternalHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            // Aquí puedes agregar HttpClients si los necesitas
            // services.AddHttpClient<IUserService, UserService>();
            // services.AddHttpClient<ICartService, CartService>();

            return services;
        }
    }
}