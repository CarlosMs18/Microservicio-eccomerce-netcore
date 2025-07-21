using Cart.Application.Contracts.Persistence;
using Cart.Infrastructure.BackgroundServices;
using Cart.Infrastructure.Configuration;
using Cart.Infrastructure.Extensions;
using Cart.Infrastructure.Infrastructure;
using Cart.Infrastructure.Persistence;
using Cart.Infrastructure.Repositories;
using Cart.Infrastructure.Services.Internal;
using Cart.Infrastructure.Services.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;

namespace Cart.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            string environment)
        {
            // 1. Configuración centralizada
            var cartConfig = EnvironmentConfigurationProvider.GetConfiguration(configuration, environment);
            services.AddSingleton(cartConfig);

            // 2. Configuración de la base de datos
            ConfigureDatabase(services, cartConfig, environment);

            // 3. Políticas de resiliencia
            services.AddResiliencePolicies();

            // 4. Registro de repositorios
            RegisterRepositories(services);

            services.AddCartScopedServices();

            // 5. Servicios externos
            ConfigureExternalServices(services, configuration, environment);

            return services;
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            CartConfiguration cartConfig,
            string environment)
        {
            services.AddDbContext<CartDbContext>((provider, options) =>
            {
                var logger = provider.GetRequiredService<ILogger<CartDbContext>>();

                options.UseSqlServer(cartConfig.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CartDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: cartConfig.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(cartConfig.Database.MaxRetryDelaySeconds),
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
                    provider.GetRequiredService<CartDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>(),
                    provider.GetRequiredService<ICartItemRepository>(),
                    provider.GetRequiredService<ICartRepository>()));

            services.AddScoped<ICartItemRepository>(provider =>
                new CartItemRepository(
                    provider.GetRequiredService<CartDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped<ICartRepository>(provider =>
               new CartRepository(
                   provider.GetRequiredService<CartDbContext>(),
                   provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
        }

        private static void ConfigureExternalServices(IServiceCollection services, IConfiguration configuration, string environment)
        {
            // gRPC Clients
            services.AddCartGrpcClients(configuration);

            // RabbitMQ Services
            services.AddRabbitMQServices(configuration, environment);

            // HTTP Clients (si los tienes)
            services.AddExternalHttpClients(configuration);
        }
    }

    // Extensiones para servicios específicos
    public static class ExternalServicesExtensions
    {
        public static IServiceCollection AddRabbitMQServices(this IServiceCollection services, IConfiguration configuration, string environment)
        {
            // Configuración de RabbitMQ
            var rabbitConfig = RabbitMQConfiguration.BuildFromConfiguration(configuration, environment);
            services.AddSingleton(rabbitConfig);

            // Consumers
            services.AddScoped<ProductPriceChangedConsumer>();

            // Background Service
            services.AddHostedService<RabbitMQConsumerHostedService>();

            return services;
        }

        public static IServiceCollection AddExternalHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            // Aquí puedes agregar HttpClients si los necesitas
            // services.AddHttpClient<IUserService, UserService>();
            // services.AddHttpClient<ICatalogService, CatalogService>();

            return services;
        }

    }

    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddCartScopedServices(this IServiceCollection services)
        {
            return services
                // Servicios para Grafana/Prometheus
                .AddSingleton<IMetricsService, CartMetricsService>()
                .AddHostedService<MetricsInitializationService>();
        }
    }
}