using Catalog.Infrastructure.Configuration;
using Catalog.Infrastructure.Services.External.Messaging;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests.Common
{
    // Evento de prueba simple
    public class TestProductPriceChangedEvent
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
    }

    public class RabbitMQPublisherBasicTest : IClassFixture<RabbitMQTestFixture>
    {
        private readonly RabbitMQTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RabbitMQPublisherBasicTest(RabbitMQTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task PublishAsync_ShouldWork_WithoutErrors()
        {
            // Arrange
            _output.WriteLine("🚀 Iniciando test básico de RabbitMQ Publisher...");

            var logger = CreateLogger();

            // ✅ USAR tu método dinámico con la configuración del fixture
            var config = RabbitMQConfiguration.BuildFromConfiguration(
                _fixture.Configuration,
                "Testing"
            );

            // Debug info
            _output.WriteLine($"🔧 Host: {config.Host}");
            _output.WriteLine($"🔧 Port: {config.Port}");
            _output.WriteLine($"🔧 ConnectionString: {config.ConnectionString}");

            using var publisher = new RabbitMQEventPublisher(config, logger);

            var testEvent = new TestProductPriceChangedEvent
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Producto Test",
                OldPrice = 100.00m,
                NewPrice = 150.00m,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "test-user",
                CategoryId = Guid.NewGuid()
            };

            _output.WriteLine($"📦 Evento creado: {testEvent.ProductName}");

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await publisher.PublishAsync(testEvent);
            });

            // Verificar que no hubo excepción
            Assert.Null(exception);

            _output.WriteLine("✅ ¡Evento publicado exitosamente! 🎉");
        }

        [Fact]
        public async Task DiagnosticTest_RabbitMQContainer()
        {
            _output.WriteLine("🔍 DIAGNÓSTICO DETALLADO DEL CONTENEDOR");

            // Información básica
            _output.WriteLine($"📦 Container State: {_fixture.RabbitMqContainer.State}");
            _output.WriteLine($"🏥 Container Health: {_fixture.RabbitMqContainer.Health}");
            _output.WriteLine($"🌐 Hostname: {_fixture.RabbitMqContainer.Hostname}");

            // Puertos
            var amqpPort = _fixture.RabbitMqContainer.GetMappedPublicPort(5672);
            var mgmtPort = _fixture.RabbitMqContainer.GetMappedPublicPort(15672);

            _output.WriteLine($"🔌 AMQP Port: {amqpPort}");
            _output.WriteLine($"🔌 Management Port: {mgmtPort}");

            // Connection string
            var connectionString = _fixture.GetConnectionString();
            _output.WriteLine($"🔗 Connection String: {connectionString}");

            // Logs del contenedor
            try
            {
                var logs = await _fixture.RabbitMqContainer.GetLogsAsync();
                _output.WriteLine("📝 Container Logs (últimas 20 líneas):");
                var logLines = logs.Stdout.Split('\n').TakeLast(20);
                foreach (var line in logLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        _output.WriteLine($"   {line}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Error obteniendo logs: {ex.Message}");
            }

            // Test manual de Management API
            try
            {
                using var httpClient = new HttpClient();
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("guest:guest"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var mgmtUrl = $"http://{_fixture.RabbitMqContainer.Hostname}:{mgmtPort}/api/overview";
                _output.WriteLine($"🌐 Probando Management API: {mgmtUrl}");

                var response = await httpClient.GetAsync(mgmtUrl);
                _output.WriteLine($"📊 Management API Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _output.WriteLine($"✅ Management API Response: {content.Substring(0, Math.Min(200, content.Length))}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Error probando Management API: {ex.Message}");
            }
        }
        private ILogger<RabbitMQEventPublisher> CreateLogger()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            return loggerFactory.CreateLogger<RabbitMQEventPublisher>();
        }
    }
}