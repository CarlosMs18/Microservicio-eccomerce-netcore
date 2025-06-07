using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Catalog.Infrastructure.Services.External.Grpc;
using Catalog.Infrastructure.Services.External.Grpc.Interceptors;
using Catalog.Infrastructure.SyncDataServices.Grpc;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
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
            // 1. Configuración de la base de datos
            ConfigureDatabase(services, configuration, environment);

            // 2. Políticas de resiliencia
            services.AddResiliencePolicies();

            // 3. Registro de repositorios
            RegisterRepositories(services);

            // 4. Configuración completa de gRPC (Cliente + Servidor)
            ConfigureGrpcServices(services, configuration);

            // 5. Configuración de RabbitMQ
            services.AddRabbitMQMessaging(configuration, environment);
            return services;
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            IConfiguration configuration,
            string environment)
        {
            var connectionString = GetConnectionString(configuration, environment);

            services.AddDbContext<CatalogDbContext>((provider, options) =>
            {
                var logger = provider.GetRequiredService<ILogger<CatalogDbContext>>();

                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
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

        private static string GetConnectionString(IConfiguration configuration, string environment)
        {
            try
            {
                var connectionParams = configuration.GetSection("ConnectionParameters");
                var poolingParams = configuration.GetSection("ConnectionPooling");
                var templates = configuration.GetSection("ConnectionTemplates");

                string template;
                var parameters = new Dictionary<string, string>();

                // Parámetros comunes de pooling (con valores por defecto)
                var commonPoolingParams = new Dictionary<string, string>
                {
                    ["pooling"] = poolingParams["pooling"] ?? "true",
                    ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                    ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                    ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                    ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
                };

                switch (environment)
                {
                    case "Development":
                        template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");
                        parameters = new Dictionary<string, string>
                        {
                            ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                            ["database"] = configuration["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                            ["trusted"] = connectionParams["trusted"] ?? "true"
                        };
                        // Agregar parámetros de pooling
                        foreach (var poolParam in commonPoolingParams)
                        {
                            parameters[poolParam.Key] = poolParam.Value;
                        }
                        break;

                    case "Docker":
                        template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");
                        parameters = new Dictionary<string, string>
                        {
                            ["server"] = connectionParams["server"] ?? "host.docker.internal,1433",
                            ["database"] = configuration["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                            ["user"] = connectionParams["user"] ?? "sa",
                            ["password"] = connectionParams["password"] ?? throw new InvalidOperationException("Password requerido para Docker"),
                            ["trust"] = connectionParams["trust"] ?? "true"
                        };
                        // Agregar parámetros de pooling
                        foreach (var poolParam in commonPoolingParams)
                        {
                            parameters[poolParam.Key] = poolParam.Value;
                        }
                        break;

                    case "Kubernetes":
                        template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");
                        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

                        if (string.IsNullOrEmpty(dbPassword))
                        {
                            throw new InvalidOperationException("Variable de entorno DB_PASSWORD no encontrada para Kubernetes");
                        }

                        parameters = new Dictionary<string, string>
                        {
                            ["server"] = connectionParams["server"] ?? throw new InvalidOperationException("Server no configurado para Kubernetes"),
                            ["database"] = configuration["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                            ["user"] = connectionParams["user"] ?? "sa",
                            ["password"] = dbPassword,
                            ["trust"] = connectionParams["trust"] ?? "true"
                        };
                        // Agregar parámetros de pooling específicos para Kubernetes (más conservadores)
                        parameters["pooling"] = poolingParams["pooling"] ?? "true";
                        parameters["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "50"; // Más conservador en K8s
                        parameters["minPoolSize"] = poolingParams["minPoolSize"] ?? "2";
                        parameters["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30";
                        parameters["commandTimeout"] = poolingParams["commandTimeout"] ?? "60"; // Más tiempo en K8s
                        break;

                    default:
                        throw new InvalidOperationException($"Entorno '{environment}' no soportado");
                }

                var connectionString = parameters.Aggregate(template, (current, param) =>
                    current.Replace($"{{{param.Key}}}", param.Value));

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Cadena de conexión no puede estar vacía");
                }

                return connectionString;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al construir connection string para {environment}: {ex.Message}", ex);
            }
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

        private static void ConfigureGrpcServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Configuración de Cliente gRPC (hacia User Service)
            ConfigureGrpcClients(services, configuration);

            // 2. Configuración de Servidor gRPC (Catalog Service)
            ConfigureGrpcServer(services, configuration);
        }

        private static void ConfigureGrpcClients(
            IServiceCollection services,
            IConfiguration configuration)
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

        private static void ConfigureGrpcServer(
            IServiceCollection services,
            IConfiguration configuration)
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

            // Health Checks para gRPC (opcional)
            // services.AddGrpcHealthChecks()
            //        .AddCheck("catalog_grpc", () => HealthCheckResult.Healthy("Catalog gRPC service is healthy"));
        }
    }
}