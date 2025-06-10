using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using User.Application.Models;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Extensions
{
    // Extension para verificación de base de datos
    public static class DatabaseExtensions
    {
        public static async Task EnsureDatabaseAsync(this IServiceProvider services, string environment)
        {
            if (environment is not ("Development" or "Docker" or "Kubernetes"))
                return;

            using var scope = services.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var retryCount = 0;
            const int maxRetries = 10;

            while (retryCount < maxRetries)
            {
                try
                {
                    var context = scopedServices.GetRequiredService<UserIdentityDbContext>();

                    Log.Information("🔄 Creando/migrando base de datos...");
                    await context.Database.MigrateAsync();

                    Log.Information("📊 Inicializando datos...");
                    await DbInitializer.InitializeAsync(
                        context,
                        scopedServices.GetRequiredService<UserManager<ApplicationUser>>(),
                        scopedServices.GetRequiredService<RoleManager<ApplicationRole>>()
                    );

                    Log.Information("🆗 Base de datos lista");
                    Log.Information("👥 Usuarios: {UserCount}, Roles: {RoleCount}",
                        context.Users.Count(),
                        context.Roles.Count());
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Log.Warning(ex, "❌ Intento {Retry}/{MaxRetries} - Error: {Message}",
                        retryCount, maxRetries, ex.Message);

                    if (retryCount >= maxRetries)
                    {
                        Log.Fatal(ex, "❌ Error crítico con BD después de {MaxRetries} intentos", maxRetries);
                        throw;
                    }

                    var delaySeconds = 5 * retryCount;
                    Log.Information("⏳ Reintentando en {Delay} segundos...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }
    }

    // Extension para logging de configuración
    public static class ConfigurationLoggingExtensions
    {
        public static void LogEndpointsConfiguration(this IConfiguration config, string environment, int restPort, int grpcPort)
        {
            try
            {
                var connectionParams = config.GetSection("ConnectionParameters");
                var databaseName = config["User:DatabaseName"] ?? "UserDB_Dev";
                var serverName = connectionParams["server"] ?? "Unknown";

                Log.Information("🗃️ DB para {Environment}: {Database} en {Server}", environment, databaseName, serverName);

                Log.Information("🌐 Endpoints configurados:");
                Log.Information("  REST API: http://localhost:{RestPort}/api/User/", restPort);
                Log.Information("  gRPC Service: http://localhost:{GrpcPort}", grpcPort);
                Log.Information("  Health Check: http://localhost:{RestPort}/health", restPort);

                // Log de configuración de servicios externos
                LogMicroservicesConfiguration(config);

                Log.Information("🔐 Configuración de Identity:");
                LogIdentityConfiguration(config);

                Log.Information("📡 Configuración de gRPC:");
                LogGrpcConfiguration(config);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error al mostrar configuración de endpoints");
            }
        }

        private static void LogMicroservicesConfiguration(IConfiguration config)
        {
            var microservicesConfig = config.GetSection("Microservices");

            // Cart Service
            var cartConfig = microservicesConfig.GetSection("Cart");
            var cartHttpTemplate = cartConfig["HttpTemplate"] ?? "http://{host}/api/Cart/";
            var cartGrpcTemplate = cartConfig["GrpcTemplate"] ?? "http://{host}:{port}";

            // Catalog Service
            var catalogConfig = microservicesConfig.GetSection("Catalog");
            var catalogHttpTemplate = catalogConfig["HttpTemplate"] ?? "http://{host}/api/Catalog/";
            var catalogGrpcTemplate = catalogConfig["GrpcTemplate"] ?? "http://{host}:{port}";

            // Para development, usar parámetros específicos si existen
            var serviceParams = config.GetSection("ServiceParameters");
            var host = serviceParams["host"] ?? "localhost";
            var cartPort = "5218"; // Puerto por defecto del Cart
            var catalogPort = "7204"; // Puerto por defecto del Catalog

            var cartServiceHttpUrl = cartHttpTemplate.Replace("{host}", host);
            var cartServiceGrpcUrl = cartGrpcTemplate.Replace("{host}", host).Replace("{port}", cartPort);
            var catalogServiceHttpUrl = catalogHttpTemplate.Replace("{host}", host);
            var catalogServiceGrpcUrl = catalogGrpcTemplate.Replace("{host}", host).Replace("{port}", catalogPort);

            Log.Information("  Cart Service HTTP: {CartHttpUrl}", cartServiceHttpUrl);
            Log.Information("  Cart Service gRPC: {CartGrpcUrl}", cartServiceGrpcUrl);
            Log.Information("  Catalog Service HTTP: {CatalogHttpUrl}", catalogServiceHttpUrl);
            Log.Information("  Catalog Service gRPC: {CatalogGrpcUrl}", catalogServiceGrpcUrl);
        }

        private static void LogIdentityConfiguration(IConfiguration config)
        {
            var jwtSettings = config.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"] ?? "UserService";
            var audience = jwtSettings["Audience"] ?? "UserService";
            var expiryMinutes = jwtSettings["ExpiryMinutes"] ?? "60";

            Log.Information("  JWT Issuer: {Issuer}", issuer);
            Log.Information("  JWT Audience: {Audience}", audience);
            Log.Information("  JWT Expiry: {ExpiryMinutes} minutos", expiryMinutes);
        }

        private static void LogGrpcConfiguration(IConfiguration config)
        {
            var grpcConfig = config.GetSection("Grpc");
            var enableDetailedErrors = grpcConfig.GetValue<bool>("EnableDetailedErrors", true);
            var maxMessageSize = grpcConfig.GetValue<int>("MaxMessageSizeMB", 16);

            Log.Information("  Detailed Errors: {EnableDetailedErrors}", enableDetailedErrors);
            Log.Information("  Max Message Size: {MaxMessageSize}MB", maxMessageSize);
        }
    }
}