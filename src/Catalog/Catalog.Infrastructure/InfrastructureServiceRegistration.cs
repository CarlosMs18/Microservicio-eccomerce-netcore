using Catalog.Application.Contracts.Persistence;
using Shared.Infrastructure.Interfaces;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;

using System;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Configuración inicial del logger
            var logger = InitializeLogging(services, configuration);

            // 2. Configuración de la base de datos
            ConfigureDatabase(services, configuration, logger);

            // 3. Añadir políticas de resiliencia (Polly desde Shared)
            services.AddResiliencePolicies();

            // 4. Registro de repositorios específicos y genéricos
            RegisterRepositories(services, logger);

            logger.LogInformation("✅ Todos los servicios de Catalog.Infrastructure configurados");
            return services;
        }

        // ---- Métodos auxiliares ----
        private static ILogger InitializeLogging(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole()
                       .AddConfiguration(configuration.GetSection("Logging"))
                       .AddJsonConsole(); // Para logs estructurados
            });

            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger("Catalog.Infrastructure");
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            IConfiguration configuration,
            ILogger logger)
        {
            var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
            var connectionString = GetConnectionString(configuration, isKubernetes, logger);

            services.AddDbContext<CatalogDbContext>((provider, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });

                if (!provider.GetRequiredService<IHostEnvironment>().IsProduction())
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });

            logger.LogInformation("📦 Database configurada para el entorno: {Environment}",
                isKubernetes ? "Kubernetes" : "Development/Docker");
        }

        private static string GetConnectionString(
            IConfiguration configuration,
            bool isKubernetes,
            ILogger logger)
        {
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            var connectionString = configuration.GetConnectionString("CatalogConnection");

            if (isKubernetes && string.IsNullOrEmpty(dbPassword))
            {
                logger.LogError("🚨 DB_PASSWORD no configurado en Kubernetes");
                throw new ArgumentNullException(nameof(dbPassword), "DB_PASSWORD es requerido en Kubernetes");
            }

            return isKubernetes
                ? string.Format(connectionString, dbPassword)
                : connectionString;
        }

        private static void RegisterRepositories(IServiceCollection services, ILogger logger)
        {
            // UnitOfWork con resiliencia
            services.AddScoped<IUnitOfWork>(provider =>
                new UnitOfWork(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>(),
                    provider.GetRequiredService<ICategoryRepository>()));

            // Repositorio específico para Category (con política de reintentos)
            services.AddScoped<ICategoryRepository>(provider =>
                new CategoryRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            // Repositorio genérico
            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

            logger.LogInformation("📦 Repositorios registrados: ICategoryRepository, IAsyncRepository<>");
        }
    }
}