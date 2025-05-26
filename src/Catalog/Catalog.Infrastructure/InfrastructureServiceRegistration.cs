using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using System;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration
            )
        {
            // 1. Configuración inicial del logger
           // var logger = InitializeLogging(services, configuration);

            // 2. Configuración de la base de datos
            ConfigureDatabase(services, configuration);

            // 3. Añadir políticas de resiliencia (Polly desde Shared)
            services.AddResiliencePolicies();

            // 4. Registro de repositorios
            RegisterRepositories(services);

            
            return services;
        }

        private static ILogger InitializeLogging(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"))
                       .AddConsole(); // Eliminado AddJsonConsole para mostrar logs más legibles
            });

            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger("Catalog.Infrastructure");
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            IConfiguration configuration
           )
        {
            var environment = DetectEnvironment();
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

                if (environment == AppEnvironment.Local)
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                    logger.LogDebug("🔍 Habilitados logs detallados de EF Core para desarrollo");
                }
            });

            
        }

        private enum AppEnvironment { Local, Docker, Kubernetes }

        private static AppEnvironment DetectEnvironment()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
                return AppEnvironment.Kubernetes;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
                return AppEnvironment.Docker;

            return AppEnvironment.Local;
        }

        private static string GetConnectionString(
            IConfiguration configuration,
            AppEnvironment environment
           )
        {
            try
            {
                switch (environment)
                {
                    case AppEnvironment.Local:
                        var localConnection = configuration.GetConnectionString("CatalogConnection");
                       
                        return localConnection;

                    case AppEnvironment.Docker:
                        var dockerConnection = configuration.GetConnectionString("DockerConnection") ??
                                               configuration.GetConnectionString("CatalogConnection");
                      
                        return dockerConnection;

                    case AppEnvironment.Kubernetes:
                        var k8sTemplate = configuration.GetConnectionString("KubernetesConnection") ??
                                          configuration.GetConnectionString("CatalogConnection");
                        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

                        if (string.IsNullOrEmpty(dbPassword))
                        {
                           
                            throw new ArgumentNullException(nameof(dbPassword), "Se requiere DB_PASSWORD en Kubernetes");
                        }

                        var k8sConnection = string.Format(k8sTemplate, dbPassword);
                        
                        return k8sConnection;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(environment));
                }
            }
            catch (Exception ex)
            {
                
                throw;
            }
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            try
            {
                // UnitOfWork con resiliencia
                services.AddScoped<IUnitOfWork>(provider =>
                    new UnitOfWork(
                        provider.GetRequiredService<CatalogDbContext>(),
                        provider.GetRequiredService<ILogger<UnitOfWork>>(),
                        provider.GetRequiredService<ICategoryRepository>()));

                // Repositorio específico para Category
                services.AddScoped<ICategoryRepository>(provider =>
                    new CategoryRepository(
                        provider.GetRequiredService<CatalogDbContext>(),
                        provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

                // Repositorio genérico
                services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

              
            }
            catch (Exception ex)
            {
                
                throw;
            }
        }
    }
}
