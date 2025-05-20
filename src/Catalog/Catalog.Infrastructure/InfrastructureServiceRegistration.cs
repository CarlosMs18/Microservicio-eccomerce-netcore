using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Catalog.Infrastructure.SyncDataServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Interfaces;
using System;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configuración del logger
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
            var logger = loggerFactory.CreateLogger("CatalogInfrastructure");

            try
            {
                logger.LogInformation("Configurando servicios de infraestructura para Catalog...");

                // 1. Configuración de la DB con manejo dinámico de contraseña
                services.AddDbContext<CatalogDbContext>(options =>
                {
                    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
                    var connectionString = configuration.GetConnectionString("CatalogConnection");

                    if (isKubernetes && string.IsNullOrEmpty(dbPassword))
                    {
                        logger.LogError("DB_PASSWORD no está configurado en Kubernetes");
                        throw new ArgumentNullException(nameof(dbPassword), "DB_PASSWORD no está configurado para Kubernetes");
                    }

                    // Formatear la cadena de conexión solo si estamos en Kubernetes
                    var formattedConnectionString = isKubernetes
                        ? string.Format(connectionString, dbPassword)
                        : connectionString;

                    // Log sanitizado para diagnóstico
                    var logConnectionString = isKubernetes
                        ? connectionString.Replace("{0}", "*****")
                        : connectionString;

                    logger.LogInformation("Cadena de conexión a BD: {ConnectionString}", logConnectionString);

                    options.UseSqlServer(formattedConnectionString, sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(120);
                        sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });

                    if (!isKubernetes)
                    {
                        options.EnableDetailedErrors();
                        options.EnableSensitiveDataLogging();
                    }
                });

                // 2. Configuración de repositorios
                services.AddScoped<IUnitOfWork, UnitOfWork>();
                services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

                // 3. Servicios de sincronización
                //services.AddScoped<IExternalCatalogService, CatalogHttpService>();

                logger.LogInformation("Servicios de infraestructura configurados correctamente");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error al configurar los servicios de infraestructura");
                throw;
            }

            return services;
        }
    }
}