using Catalog.Application.Contracts.Messaging;
using Catalog.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;  // ✅ SOLO este using para logging
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
// ❌ REMOVIDO: using Serilog; - Ya no lo necesitas

namespace Catalog.Infrastructure.Services.External.Messaging
{
    public class RabbitMQEventPublisher : IEventPublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQEventPublisher> _logger;  // ✅ Ya está correcto
        private readonly RabbitMQConfiguration _config;
        private readonly string _exchangeName;
        private bool _disposed = false;

        public RabbitMQEventPublisher(RabbitMQConfiguration configuration, ILogger<RabbitMQEventPublisher> logger)
        {
            _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
            _exchangeName = "catalog.events";

            try
            {
                var factory = CreateConnectionFactory();
                _connection = factory.CreateConnection("Catalog-EventPublisher");
                _channel = _connection.CreateModel();

                SetupExchangeAndQueues();
                _logger.LogCritical("🔍 EXCHANGE CONFIGURADO: {ExchangeName}", _exchangeName);
                _logger.LogInformation("✅ RabbitMQ EventPublisher inicializado correctamente para {ExchangeName}", _exchangeName);
                _logger.LogDebug("🔧 Configuración: {Host}:{Port}, VHost: {VHost}",
                    _config.Host, _config.Port, _config.VirtualHost);  // ✅ Cambiado a LogDebug
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al inicializar RabbitMQ EventPublisher");
                throw;
            }
        }

        public async Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : class
        {
            _logger.LogDebug("🐰 LLAMANDO AL PUBLISHASYNC");  // ✅ Cambiado a LogDebug

            if (_disposed)
                throw new ObjectDisposedException(nameof(RabbitMQEventPublisher));
            if (eventMessage == null)
                throw new ArgumentNullException(nameof(eventMessage));

            try
            {
                var eventName = typeof(T).Name;
                var routingKey = GenerateRoutingKey(eventName);
                var message = SerializeMessage(eventMessage);
                var body = Encoding.UTF8.GetBytes(message);
                var properties = CreateMessageProperties(eventName);

                // 🎯 LOGGING DETALLADO ANTES DEL ENVÍO - TODOS CORREGIDOS
                _logger.LogDebug("🐰 ===== ENVIANDO EVENTO =====");
                _logger.LogCritical("📋 Tipo: {EventName}", eventName);  // ✅ LogCritical para que se vea como Fatal
                _logger.LogInformation("🔑 Routing Key: {RoutingKey}", routingKey);
                _logger.LogInformation("📨 Exchange: {Exchange}", _exchangeName);
                _logger.LogInformation("📄 JSON del Evento: {EventJson}",
                    JsonConvert.SerializeObject(eventMessage, Formatting.Indented));
                _logger.LogInformation("================================");

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("📤 Evento {EventName} publicado exitosamente con routing key {RoutingKey} en exchange {Exchange}",
                    eventName, routingKey, _exchangeName);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al publicar evento {EventType}: {ErrorMessage}",
                    typeof(T).Name, ex.Message);  // ✅ Cambiado a LogError
                throw;
            }
        }

        private void SetupExchangeAndQueues()
        {
            // Exchange principal para eventos del catálogo
            _channel.ExchangeDeclare(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Exchange para Dead Letter Queue
            _channel.ExchangeDeclare(
                exchange: $"{_exchangeName}.dlx",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            _logger.LogDebug("🔧 Exchanges configurados: {MainExchange} y {DlxExchange}",
                _exchangeName, $"{_exchangeName}.dlx");  // ✅ Cambiado a LogDebug
        }

        private static string GenerateRoutingKey(string eventName)
        {
            // Mapeo específico para eventos siguiendo convenciones estándar
            var routingKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ProductPriceChangedEvent", "catalog.product.updated" },
                { "ProductCreatedEvent", "catalog.product.created" },
                { "ProductDeletedEvent", "catalog.product.deleted" },
                { "ProductUpdatedEvent", "catalog.product.updated" },
                { "CategoryCreatedEvent", "catalog.category.created" },
                { "CategoryUpdatedEvent", "catalog.category.updated" },
                { "CategoryDeletedEvent", "catalog.category.deleted" }
            };

            // Si existe un mapeo específico, usarlo (mejor práctica)
            if (routingKeyMap.TryGetValue(eventName, out var routingKey))
            {
                return routingKey;
            }

            // Fallback para eventos no mapeados
            return eventName.ToLowerInvariant()
                .Replace("event", "")
                .Replace("changed", "updated")
                .Replace("updated", "updated")
                .Replace("created", "created")
                .Replace("deleted", "deleted")
                .Insert(0, "catalog.");
        }

        private static string SerializeMessage<T>(T eventMessage)
        {
            return JsonConvert.SerializeObject(eventMessage, new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });
        }

        private IBasicProperties CreateMessageProperties(string eventName)
        {
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.ContentType = "application/json";
            properties.ContentEncoding = "utf-8";
            properties.Type = eventName;
            properties.AppId = "Catalog.Service";

            properties.Headers = new Dictionary<string, object>
            {
                ["source"] = "catalog-service",
                ["version"] = "1.0",
                ["published_at"] = DateTime.UtcNow.ToString("O")
            };

            return properties;
        }

        private ConnectionFactory CreateConnectionFactory()
        {
            return new ConnectionFactory
            {
                HostName = _config.Host,
                Port = _config.Port,
                UserName = _config.Username,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,

                // Configuraciones de producción usando la configuración
                AutomaticRecoveryEnabled = _config.AutomaticRecoveryEnabled,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(_config.NetworkRecoveryIntervalSeconds),
                RequestedHeartbeat = TimeSpan.FromSeconds(_config.RequestedHeartbeatSeconds),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),

                DispatchConsumersAsync = true
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();

                _logger.LogInformation("🔌 RabbitMQ EventPublisher desconectado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error al cerrar conexión RabbitMQ: {ErrorMessage}", ex.Message);  // ✅ Cambiado a LogWarning
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}