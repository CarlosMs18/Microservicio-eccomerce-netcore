using Cart.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using Cart.Application.Contracts.Persistence;
using Cart.Infrastructure.Repositories;

namespace Cart.Infrastructure
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

            services.AddDbContext<CartDbContext>((provider, options) =>
            {
                var logger = provider.GetRequiredService<ILogger<CartDbContext>>();

                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(CartDbContext).Assembly.FullName);
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
                            ["database"] = configuration["Cart:DatabaseName"] ?? "CartDB_Dev",
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
                            ["database"] = configuration["Cart:DatabaseName"] ?? "CartDB_Dev",
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
                            ["database"] = configuration["Cart:DatabaseName"] ?? "CartDB_Dev",
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
    }
}