using Testcontainers.RabbitMq;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace Catalog.IntegrationTests.Fixtures
{
    public class RabbitMQTestFixture : IAsyncLifetime
    {
        public RabbitMqContainer RabbitMqContainer { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            // 1. Crear el contenedor de RabbitMQ
            RabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .Build();

            // 2. Iniciar el contenedor
            await RabbitMqContainer.StartAsync();

            // 3. Esperar que RabbitMQ esté completamente listo
            await Task.Delay(3000);

            // 4. Crear configuración compatible con tu sistema
            Configuration = BuildTestConfiguration();
        }

        public async Task DisposeAsync()
        {
            if (RabbitMqContainer != null)
            {
                await RabbitMqContainer.StopAsync();
                await RabbitMqContainer.DisposeAsync();
            }
        }

        /// <summary>
        /// Crea una configuración que sea compatible con tu RabbitMQConfiguration.BuildFromConfiguration
        /// </summary>
        private IConfiguration BuildTestConfiguration()
        {
            var configData = new Dictionary<string, string>
            {
                // Configuración base (como en tu appsettings.json)
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/",
                ["RabbitMQ:AutomaticRecoveryEnabled"] = "true",
                ["RabbitMQ:NetworkRecoveryIntervalSeconds"] = "10",
                ["RabbitMQ:RequestedHeartbeatSeconds"] = "60",

                // Template (como en tu appsettings.json)
                ["RabbitMQTemplates:Default"] = "amqp://{username}:{password}@{host}:{port}/{virtualhost}",

                // Parámetros específicos del contenedor (sobreescriben los valores base)
                ["RabbitMQParameters:host"] = RabbitMqContainer.Hostname,
                ["RabbitMQParameters:port"] = RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["RabbitMQParameters:username"] = "guest",
                ["RabbitMQParameters:password"] = "guest",
                ["RabbitMQParameters:virtualhost"] = "/",

                // Otras configuraciones que podrías necesitar en tests
                ["HttpClientPolicies:RetryCount"] = "1",
                ["HttpClientPolicies:RetryDelaySec"] = "1",
                ["HttpClientPolicies:CircuitBreakerFailures"] = "3",
                ["HttpClientPolicies:CircuitBreakerDurationSec"] = "10"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        /// <summary>
        /// Método para obtener la configuración de RabbitMQ del contenedor (compatibilidad hacia atrás)
        /// </summary>
        public Dictionary<string, string> GetRabbitMQConfiguration()
        {
            return new Dictionary<string, string>
            {
                ["RabbitMQParameters:host"] = RabbitMqContainer.Hostname,
                ["RabbitMQParameters:port"] = RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["RabbitMQParameters:username"] = "guest",
                ["RabbitMQParameters:password"] = "guest",
                ["RabbitMQParameters:virtualhost"] = "/"
            };
        }

        /// <summary>
        /// Método para obtener el ConnectionString directo (útil para tests simples)
        /// </summary>
        public string GetConnectionString()
        {
            var host = RabbitMqContainer.Hostname;
            var port = RabbitMqContainer.GetMappedPublicPort(5672);
            return $"amqp://guest:guest@{host}:{port}/";
        }
    }
}