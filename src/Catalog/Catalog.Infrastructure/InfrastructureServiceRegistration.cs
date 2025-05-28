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
            string environment)
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
                var templates = configuration.GetSection("ConnectionTemplates");

                string template;
                var parameters = new Dictionary<string, string>();

                switch (environment)
                {
                    case "Development":
                        // Para Development - usar LocalDB con Trusted Connection
                        template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");
                        parameters = new Dictionary<string, string>
                        {
                            ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                            ["database"] = configuration["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                            ["trusted"] = connectionParams["trusted"] ?? "true"
                        };
                        break;

                    case "Docker":
                        // Para Docker - usar SQL Server con usuario/password
                        template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");
                        parameters = new Dictionary<string, string>
                        {
                            ["server"] = connectionParams["server"] ?? "host.docker.internal,1433",
                            ["database"] = configuration["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                            ["user"] = connectionParams["user"] ?? "sa",
                            ["password"] = connectionParams["password"] ?? throw new InvalidOperationException("Password requerido para Docker"),
                            ["trust"] = connectionParams["trust"] ?? "true"
                        };
                        break;

                    case "Kubernetes":
                        // Para Kubernetes - usar SA authentication con variable de entorno
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
                    provider.GetRequiredService<ICategoryRepository>()));

            services.AddScoped<ICategoryRepository>(provider =>
                new CategoryRepository(
                    provider.GetRequiredService<CatalogDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
        }
    }
}