using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            string environment) // ← Recibimos environment como parámetro
        {
            // 1. Configuración de la base de datos
            ConfigureDatabase(services, configuration, environment);

            // 2. Políticas de resiliencia
            services.AddResiliencePolicies();

            // 3. Registro de repositorios
            RegisterRepositories(services);

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

                if (environment == "Local")
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                    logger.LogDebug("🔍 Habilitados logs detallados de EF Core");
                }
            });
        }

        private static string GetConnectionString(
            IConfiguration configuration,
            string environment)
        {
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

            return environment switch
            {
                "Kubernetes" => string.Format(
                    configuration.GetConnectionString("KubernetesConnection") ??
                    configuration.GetConnectionString("CatalogConnection")!,
                    dbPassword ?? throw new ArgumentNullException(nameof(dbPassword),
                    "DB_PASSWORD requerido en Kubernetes")),
                "Docker" => configuration.GetConnectionString("DockerConnection") ??
                            configuration.GetConnectionString("CatalogConnection")!,
                _ => configuration.GetConnectionString("CatalogConnection")!
            };
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork>(provider =>
                new UnitOfWork(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>(),
                    provider.GetRequiredService<ICategoryRepository>()));

            services.AddScoped<ICategoryRepository>(provider =>
                new CategoryRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
        }
    }
}