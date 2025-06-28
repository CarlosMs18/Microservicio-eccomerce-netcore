using Testcontainers.RabbitMq;
using Xunit;
using System.Net.Http;
using System.Text;
using RabbitMQ.Client;

namespace Catalog.IntegrationTests.Fixtures
{
    public class RabbitMQTestFixture : IAsyncLifetime
    {
        public RabbitMqContainer RabbitMqContainer { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Console.WriteLine("🚀 Iniciando RabbitMQ TestContainer...");

            // 1. Crear el contenedor de RabbitMQ
            RabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3.11-management")
                .WithUsername("guest")
                .WithPassword("guest")
                .WithPortBinding(5672, true) // Puerto AMQP dinámico
                .WithPortBinding(15672, true) // Puerto Management dinámico
                .Build();

            // 2. Iniciar el contenedor
            await RabbitMqContainer.StartAsync();

            // 3. Obtener puertos dinámicos
            var amqpPort = RabbitMqContainer.GetMappedPublicPort(5672);
            var mgmtPort = RabbitMqContainer.GetMappedPublicPort(15672);
            var host = RabbitMqContainer.Hostname;

            Console.WriteLine($"🔧 RabbitMQ Container iniciado:");
            Console.WriteLine($"   - Host: {host}");
            Console.WriteLine($"   - AMQP Port: {amqpPort}");
            Console.WriteLine($"   - Management Port: {mgmtPort}");

            // 4. ✅ CONFIGURAR ENVIRONMENT VARIABLES para que tu app las use
            Environment.SetEnvironmentVariable("RabbitMQParameters__host", host);
            Environment.SetEnvironmentVariable("RabbitMQParameters__port", amqpPort.ToString());
            Environment.SetEnvironmentVariable("RabbitMQParameters__username", "guest");
            Environment.SetEnvironmentVariable("RabbitMQParameters__password", "guest");
            Environment.SetEnvironmentVariable("RabbitMQParameters__virtualhost", "/");

            Console.WriteLine("🔧 Environment variables configuradas:");
            Console.WriteLine($"   - RabbitMQ__Host = {host}");
            Console.WriteLine($"   - RabbitMQ__Port = {amqpPort}");

            // 5. Esperar hasta que RabbitMQ esté listo
            await WaitForRabbitMQReady();

            // 6. Crear exchanges necesarios para testing
            await SetupTestExchanges();

            Console.WriteLine("✅ RabbitMQ TestContainer listo para usar!");
        }

        /// <summary>
        /// Espera hasta que RabbitMQ esté completamente listo
        /// </summary>
        private async Task WaitForRabbitMQReady()
        {
            var maxAttempts = 60; // 1 minuto máximo
            var attempt = 0;

            Console.WriteLine("🔄 Esperando que RabbitMQ esté listo...");

            while (attempt < maxAttempts)
            {
                try
                {
                    var mgmtPort = RabbitMqContainer.GetMappedPublicPort(15672);
                    var mgmtUrl = $"http://{RabbitMqContainer.Hostname}:{mgmtPort}/api/overview";

                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(3);

                    // Autenticación básica
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                    var response = await httpClient.GetAsync(mgmtUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("✅ RabbitMQ Management API responde correctamente!");

                        // Esperar un poco más para asegurar estabilidad
                        await Task.Delay(2000);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🔄 Intento {attempt + 1}/{maxAttempts} - RabbitMQ no listo: {ex.Message}");
                }

                attempt++;
                await Task.Delay(1000);
            }

            throw new Exception($"❌ RabbitMQ no se inicializó después de {maxAttempts} segundos");
        }

        /// <summary>
        /// Crea los exchanges necesarios para los tests
        /// </summary>
        private async Task SetupTestExchanges()
        {
            Console.WriteLine("🔧 Configurando exchanges para testing...");

            try
            {
                var factory = new ConnectionFactory();
                factory.Uri = new Uri(GetConnectionString());

                using var connection = factory.CreateConnection("Setup-Connection");
                using var channel = connection.CreateModel();

                // Crear el exchange principal que usa tu aplicación
                channel.ExchangeDeclare(
                    exchange: "catalog.events",
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false
                );

                Console.WriteLine("✅ Exchange 'catalog.events' creado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creando exchanges: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Método helper para crear exchanges adicionales durante los tests
        /// </summary>
        public async Task CreateExchange(string exchangeName, string exchangeType = ExchangeType.Topic)
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(GetConnectionString());

            using var connection = factory.CreateConnection("Test-Exchange-Creation");
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare(
                exchange: exchangeName,
                type: exchangeType,
                durable: true,
                autoDelete: false
            );

            Console.WriteLine($"✅ Exchange '{exchangeName}' creado");
        }

        /// <summary>
        /// Método helper para obtener la connection string (útil para verificaciones en tests)
        /// </summary>
        public string GetConnectionString()
        {
            var host = RabbitMqContainer.Hostname;
            var port = RabbitMqContainer.GetMappedPublicPort(5672);
            return $"amqp://guest:guest@{host}:{port}/";
        }

        /// <summary>
        /// Método helper para limpiar colas/exchanges entre tests si necesitas
        /// </summary>
        public async Task CleanupQueues()
        {
            try
            {
                var factory = new ConnectionFactory();
                factory.Uri = new Uri(GetConnectionString());

                using var connection = factory.CreateConnection("Cleanup-Connection");
                using var channel = connection.CreateModel();

                // Aquí puedes agregar lógica para limpiar colas específicas
                // Por ejemplo: channel.QueueDelete("some.queue", false, false);

                Console.WriteLine("🧹 Colas limpiadas");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error limpiando colas: {ex.Message}");
            }
        }

        public async Task DisposeAsync()
        {
            Console.WriteLine("🧹 Limpiando RabbitMQ TestContainer...");

            if (RabbitMqContainer != null)
            {
                try
                {
                    await RabbitMqContainer.StopAsync();
                    await RabbitMqContainer.DisposeAsync();
                    Console.WriteLine("✅ TestContainer limpiado correctamente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error limpiando container: {ex.Message}");
                }
            }
        }
    }
}