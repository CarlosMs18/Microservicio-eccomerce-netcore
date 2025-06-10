using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Catalog.Infrastructure.Extensions
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
                    var db = scopedServices.GetRequiredService<CatalogDbContext>();

                    Log.Information("🔄 Creando/migrando base de datos...");
                    await db.Database.MigrateAsync();

                    Log.Information("📊 Inicializando datos...");
                    await CatalogDbInitializer.InitializeAsync(db);

                    Log.Information("🆗 Base de datos lista");
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

    // Extension para verificación de RabbitMQ
    public static class RabbitMQExtensions
    {
        public static async Task VerifyRabbitMQAsync(this IServiceProvider services, IConfiguration configuration, string environment)
        {
            try
            {
                Log.Information("🐰 Verificando conexión a RabbitMQ...");

                // Obtener configuración de RabbitMQ
                var rabbitConfig = configuration.GetSection("RabbitMQ");
                var rabbitParams = configuration.GetSection("RabbitMQParameters");

                var host = rabbitParams["host"] ?? rabbitConfig["Host"] ?? "localhost";
                var port = rabbitParams["port"] ?? rabbitConfig["Port"] ?? "5672";
                var username = rabbitParams["username"] ?? rabbitConfig["Username"] ?? "guest";
                var virtualHost = rabbitParams["virtualhost"] ?? rabbitConfig["VirtualHost"] ?? "/";

                Log.Information("🔗 Configuración RabbitMQ para {Environment}:", environment);
                Log.Information("  Host: {Host}:{Port}", host, port);
                Log.Information("  Username: {Username}", username);
                Log.Information("  Virtual Host: {VirtualHost}", virtualHost);

                Log.Information("✅ Configuración RabbitMQ cargada correctamente");

                // Test básico de conectividad usando HttpClient
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                try
                {
                    var managementUrl = $"http://{host}:15672/api/overview";
                    var response = await httpClient.GetAsync(managementUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        Log.Information("✅ RabbitMQ Management API accesible");
                    }
                    else
                    {
                        Log.Warning("⚠️ RabbitMQ Management API respondió con código: {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("⚠️ No se pudo conectar al Management API de RabbitMQ: {Message}", ex.Message);
                    Log.Information("ℹ️ Esto es normal si RabbitMQ no tiene el plugin de management habilitado");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error al verificar RabbitMQ: {Message}", ex.Message);
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
                var databaseName = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev";
                var serverName = connectionParams["server"] ?? "Unknown";

                Log.Information("🗃️ DB para {Environment}: {Database} en {Server}", environment, databaseName, serverName);

                Log.Information("🌐 Endpoints configurados:");
                Log.Information("  REST API: http://localhost:{Port}/api/v1/", restPort);
                Log.Information("  gRPC API: http://localhost:{Port}", grpcPort);

                // Log de configuración de servicios externos
                LogMicroservicesConfiguration(config);
                LogRabbitMQConfiguration(config);

                Log.Information("  Health Check: http://localhost:{Port}/health", restPort);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error al mostrar configuración de endpoints");
            }
        }

        private static void LogMicroservicesConfiguration(IConfiguration config)
        {
            var microservicesConfig = config.GetSection("Microservices");

            // User Service
            var userConfig = microservicesConfig.GetSection("User");
            var userHttpTemplate = userConfig["HttpTemplate"] ?? "http://{host}/api/User/";
            var userGrpcTemplate = userConfig["GrpcTemplate"] ?? "http://{host}:{port}";

            // Cart Service (si se comunica con él)
            var cartConfig = microservicesConfig.GetSection("Cart");
            var cartHttpTemplate = cartConfig["HttpTemplate"] ?? "http://{host}/api/Cart/";
            var cartGrpcTemplate = cartConfig["GrpcTemplate"] ?? "http://{host}:{port}";

            // Para development, usar parámetros específicos si existen
            var serviceParams = config.GetSection("ServiceParameters");
            var host = serviceParams["host"] ?? "localhost";
            var userPort = serviceParams["port"] ?? "5001";
            var cartPort = "5218";

            var userServiceHttpUrl = userHttpTemplate.Replace("{host}", host);
            var userServiceGrpcUrl = userGrpcTemplate.Replace("{host}", host).Replace("{port}", userPort);
            var catalogServiceHttpUrl = cartHttpTemplate.Replace("{host}", host);
            var catalogServiceGrpcUrl = cartGrpcTemplate.Replace("{host}", host).Replace("{port}", cartPort);

            Log.Information("  User Service HTTP: {UserHttpUrl}", userServiceHttpUrl);
            Log.Information("  User Service gRPC: {UserGrpcUrl}", userServiceGrpcUrl);
            Log.Information("  Catalog Service HTTP: {CatalogHttpUrl}", catalogServiceHttpUrl);
            Log.Information("  Catalog Service gRPC: {CatalogGrpcUrl}", catalogServiceGrpcUrl);
        }

        private static void LogRabbitMQConfiguration(IConfiguration config)
        {
            var rabbitParams = config.GetSection("RabbitMQParameters");
            var rabbitConfig = config.GetSection("RabbitMQ");
            var rabbitHost = rabbitParams["host"] ?? rabbitConfig["Host"] ?? "localhost";
            var rabbitPort = rabbitParams["port"] ?? rabbitConfig["Port"] ?? "5672";
            Log.Information("  RabbitMQ: amqp://{Host}:{Port}", rabbitHost, rabbitPort);
        }
    }
}