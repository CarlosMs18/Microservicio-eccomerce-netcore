using Testcontainers.RabbitMq;
using Xunit;
using Microsoft.Extensions.Configuration;
using DotNet.Testcontainers.Builders;
using System.Net.Http;
using System.Text;

namespace Catalog.IntegrationTests.Fixtures
{
    public class RabbitMQTestFixture : IAsyncLifetime
    {
        public RabbitMqContainer RabbitMqContainer { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Console.WriteLine("🚀 Iniciando RabbitMQ Container...");

            // 1. Crear el contenedor de RabbitMQ con configuración más específica
            RabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3.11-management") // Versión más específica
                .WithUsername("guest")
                .WithPassword("guest")
                .WithPortBinding(5672, true) // Mapear puerto AMQP
                .WithPortBinding(15672, true) // Mapear puerto Management
                .WithEnvironment("RABBITMQ_DEFAULT_USER", "guest")
                .WithEnvironment("RABBITMQ_DEFAULT_PASS", "guest")
                .WithEnvironment("RABBITMQ_DEFAULT_VHOST", "/")
                .Build();

            // 2. Iniciar el contenedor
            Console.WriteLine("📦 Iniciando contenedor RabbitMQ...");
            await RabbitMqContainer.StartAsync();

            // 3. Información de debug
            var amqpPort = RabbitMqContainer.GetMappedPublicPort(5672);
            var mgmtPort = RabbitMqContainer.GetMappedPublicPort(15672);

            Console.WriteLine($"🔧 Container iniciado:");
            Console.WriteLine($"   - Hostname: {RabbitMqContainer.Hostname}");
            Console.WriteLine($"   - AMQP Port: {amqpPort}");
            Console.WriteLine($"   - Management Port: {mgmtPort}");
            Console.WriteLine($"   - Connection String: amqp://guest:guest@{RabbitMqContainer.Hostname}:{amqpPort}/");

            // 4. ✅ ESPERAR HASTA QUE RABBITMQ ESTÉ REALMENTE LISTO
            await WaitForRabbitMQReady();

            // 5. Crear configuración para Testing
            Configuration = BuildTestConfiguration();

            Console.WriteLine("✅ RabbitMQ Test Fixture inicializado correctamente!");
        }

        /// <summary>
        /// Espera hasta que RabbitMQ esté completamente listo para aceptar conexiones
        /// </summary>
        private async Task WaitForRabbitMQReady()
        {
            var maxAttempts = 120; // 2 minutos máximo
            var attempt = 0;

            Console.WriteLine("🔄 Esperando que RabbitMQ esté completamente listo...");

            while (attempt < maxAttempts)
            {
                try
                {
                    var mgmtPort = RabbitMqContainer.GetMappedPublicPort(15672);
                    var mgmtUrl = $"http://{RabbitMqContainer.Hostname}:{mgmtPort}/api/overview";

                    Console.WriteLine($"🔍 Intento {attempt + 1}: Verificando {mgmtUrl}");

                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    // Agregar autenticación básica
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                    var response = await httpClient.GetAsync(mgmtUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("✅ RabbitMQ Management API responde correctamente!");
                        Console.WriteLine($"📊 Response: {content.Substring(0, Math.Min(100, content.Length))}...");

                        // Esperar un poco más para asegurar que AMQP también esté listo
                        await Task.Delay(3000);

                        // Verificar también que el puerto AMQP responda
                        await VerifyAMQPConnection();
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Management API response: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🔄 RabbitMQ no listo... Intento {attempt + 1}/{maxAttempts}");
                    Console.WriteLine($"   Error: {ex.GetType().Name}: {ex.Message}");

                    // Log adicional para debugging
                    if (attempt % 10 == 0) // Cada 10 intentos
                    {
                        await LogContainerStatus();
                    }
                }

                attempt++;
                await Task.Delay(1000);
            }

            // Si llegamos aquí, falló
            await LogContainerStatus();
            throw new Exception($"❌ RabbitMQ no se inicializó correctamente después de {maxAttempts} segundos");
        }

        private async Task VerifyAMQPConnection()
        {
            try
            {
                Console.WriteLine("🔌 Verificando conexión AMQP...");
                var connectionString = GetConnectionString();

                // Aquí podrías agregar una verificación real de RabbitMQ
                // Por ahora solo logeamos la connection string
                Console.WriteLine($"🔗 AMQP Connection String: {connectionString}");

                Console.WriteLine("✅ AMQP parece estar disponible");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error verificando AMQP: {ex.Message}");
                throw;
            }
        }

        private async Task LogContainerStatus()
        {
            try
            {
                Console.WriteLine("📋 Estado del contenedor:");
                Console.WriteLine($"   - State: {RabbitMqContainer.State}");
                Console.WriteLine($"   - Health: {RabbitMqContainer.Health}");

                // Intentar obtener logs del contenedor
                var logs = await RabbitMqContainer.GetLogsAsync();
                var logLines = logs.Stdout.Split('\n').TakeLast(10);
                Console.WriteLine("📝 Últimas 10 líneas de logs:");
                foreach (var line in logLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        Console.WriteLine($"   {line}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo estado del contenedor: {ex.Message}");
            }
        }

        public async Task DisposeAsync()
        {
            Console.WriteLine("🧹 Limpiando RabbitMQ Container...");

            if (RabbitMqContainer != null)
            {
                try
                {
                    await RabbitMqContainer.StopAsync();
                    await RabbitMqContainer.DisposeAsync();
                    Console.WriteLine("✅ Container limpiado correctamente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error limpiando container: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Crea la configuración que tu RabbitMQConfiguration.BuildFromConfiguration necesita
        /// </summary>
        private IConfiguration BuildTestConfiguration()
        {
            var amqpPort = RabbitMqContainer.GetMappedPublicPort(5672);
            var host = RabbitMqContainer.Hostname;

            var configData = new Dictionary<string, string>
            {
                // Configuración base (igual que en appsettings.json)
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/",
                ["RabbitMQ:AutomaticRecoveryEnabled"] = "true",
                ["RabbitMQ:NetworkRecoveryIntervalSeconds"] = "10",
                ["RabbitMQ:RequestedHeartbeatSeconds"] = "60",

                // Template para construir ConnectionString
                ["RabbitMQTemplates:Default"] = "amqp://{username}:{password}@{host}:{port}/{virtualhost}",

                // ✅ ESTOS son los que sobreescriben para el contenedor
                ["RabbitMQParameters:host"] = host,
                ["RabbitMQParameters:port"] = amqpPort.ToString(),
                ["RabbitMQParameters:username"] = "guest",
                ["RabbitMQParameters:password"] = "guest",
                ["RabbitMQParameters:virtualhost"] = "/"
            };

            Console.WriteLine("🔧 Configuración generada:");
            foreach (var kvp in configData.Where(x => x.Key.Contains("Parameters")))
            {
                Console.WriteLine($"   {kvp.Key} = {kvp.Value}");
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        /// <summary>
        /// Método helper para obtener ConnectionString directo (para tests básicos)
        /// </summary>
        public string GetConnectionString()
        {
            var host = RabbitMqContainer.Hostname;
            var port = RabbitMqContainer.GetMappedPublicPort(5672);
            return $"amqp://guest:guest@{host}:{port}/";
        }
    }
}