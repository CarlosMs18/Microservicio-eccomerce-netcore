using Cart.Infrastructure.Extensions;
using Cart.Infrastructure.Services.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cart.Infrastructure.BackgroundServices
{
    public class RabbitMQConsumerHostedService : BackgroundService
    {
        private readonly ILogger<RabbitMQConsumerHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQConfiguration _rabbitConfig;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMQConsumerHostedService(
            ILogger<RabbitMQConsumerHostedService> logger,
            IServiceProvider serviceProvider,
            RabbitMQConfiguration rabbitConfig)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _rabbitConfig = rabbitConfig;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                InitializeRabbitMQ();

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        _logger.LogInformation($"📨 Mensaje recibido - Queue: {ea.ConsumerTag}, RoutingKey: {ea.RoutingKey}, Exchange: {ea.Exchange}");
                        _logger.LogDebug($"📄 Contenido del mensaje: {message}");

                        // Procesar el mensaje
                        await ProcessMessage(message, ea.RoutingKey);

                        // Confirmar que se procesó correctamente
                        _channel.BasicAck(ea.DeliveryTag, false);

                        _logger.LogDebug($"✅ Mensaje procesado y confirmado - DeliveryTag: {ea.DeliveryTag}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Error procesando mensaje - RoutingKey: {ea.RoutingKey}");

                        // Rechazar el mensaje y enviarlo al DLX
                        _channel.BasicReject(ea.DeliveryTag, false);
                    }
                };

                // Configurar las colas a consumir
                SetupQueues();

                // Iniciar consumo con las nuevas colas
                _channel.BasicConsume(
                    queue: "cart.product.updates",
                    autoAck: false,
                    consumer: consumer);

                _channel.BasicConsume(
                    queue: "cart.product.deletions",
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("🚀 RabbitMQ Consumer iniciado correctamente - Escuchando colas: cart.product.updates, cart.product.deletions");

                // Mantener el servicio ejecutándose
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RabbitMQ Consumer");
                throw;
            }
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(_rabbitConfig.ConnectionString),
                AutomaticRecoveryEnabled = _rabbitConfig.AutomaticRecoveryEnabled,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(_rabbitConfig.NetworkRecoveryIntervalSeconds),
                RequestedHeartbeat = TimeSpan.FromSeconds(_rabbitConfig.RequestedHeartbeatSeconds)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _logger.LogInformation($"Conexión a RabbitMQ establecida: {_rabbitConfig.Host}:{_rabbitConfig.Port}");
        }

        private void SetupQueues()
        {
            // Declarar exchange principal (debe coincidir con el del publicador)
            _channel.ExchangeDeclare(
                exchange: "catalog.events",
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Declarar colas con nombres descriptivos
            var queues = new Dictionary<string, string[]>
            {
                {
                    "cart.product.updates", // Cola para todos los updates de productos
                    new[] { "catalog.product.updated", "catalog.product.created" }
                },
                {
                    "cart.product.deletions", // Cola para eliminaciones de productos
                    new[] { "catalog.product.deleted" }
                }
            };

            foreach (var (queueName, routingKeys) in queues)
            {
                // Declarar cola
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        // Dead Letter Exchange para manejo de errores
                        ["x-dead-letter-exchange"] = "catalog.events.dlx",
                        ["x-dead-letter-routing-key"] = "failed"
                    });

                // Bindear múltiples routing keys a la misma cola
                foreach (var routingKey in routingKeys)
                {
                    _channel.QueueBind(
                        queue: queueName,
                        exchange: "catalog.events",
                        routingKey: routingKey);
                }

                _logger.LogInformation($"Cola '{queueName}' configurada con routing keys: [{string.Join(", ", routingKeys)}]");
            }

            // Configurar Dead Letter Exchange
            _channel.ExchangeDeclare(
                exchange: "catalog.events.dlx",
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);
        }

        private async Task ProcessMessage(string message, string routingKey)
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                _logger.LogInformation($"🔄 Procesando mensaje con routing key: {routingKey}");

                switch (routingKey)
                {
                    // Todos los casos de actualización de productos
                    case "catalog.product.updated":
                    case "catalog.product.created":
                        await HandleProductUpdate(scope, message, routingKey);
                        break;

                    // Eliminación de productos
                    case "catalog.product.deleted":
                        await HandleProductDeletion(scope, message, routingKey);
                        break;

                    default:
                        _logger.LogWarning($"⚠️ Routing key no reconocido: {routingKey}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error procesando mensaje con routing key: {routingKey}");
                throw; // Re-lanzar para que RabbitMQ maneje el retry/DLX
            }
        }

        private async Task HandleProductUpdate(IServiceScope scope, string message, string routingKey)
        {
            try
            {
                _logger.LogInformation($"📦 Procesando actualización de producto - RoutingKey: {routingKey}");

                var priceConsumer = scope.ServiceProvider.GetRequiredService<ProductPriceChangedConsumer>();
                await priceConsumer.HandleAsync(message);

                _logger.LogInformation($"✅ Actualización de producto procesada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error procesando actualización de producto");
                throw;
            }
        }

        private async Task HandleProductDeletion(IServiceScope scope, string message, string routingKey)
        {
            try
            {
                _logger.LogInformation($"🗑️ Procesando eliminación de producto - RoutingKey: {routingKey}");

                // TODO: Implementar consumer para eliminación de productos
                // var deletionConsumer = scope.ServiceProvider.GetRequiredService<ProductDeletedConsumer>();
                // await deletionConsumer.HandleAsync(message);

                _logger.LogInformation($"⚠️ Eliminación de producto - Funcionalidad pendiente de implementar");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error procesando eliminación de producto");
                throw;
            }
        }

        public override void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _channel?.Dispose();
                _connection?.Dispose();

                _logger.LogInformation("🔌 Conexión RabbitMQ cerrada correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cerrando conexión RabbitMQ");
            }

            base.Dispose();
        }
    }
}