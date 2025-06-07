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

                        _logger.LogInformation($"Mensaje recibido: {message}");

                        // Procesar el mensaje
                        await ProcessMessage(message, ea.RoutingKey);

                        // Confirmar que se procesó correctamente
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error procesando mensaje de RabbitMQ");

                        // Rechazar el mensaje y no reenviarlo
                        _channel.BasicReject(ea.DeliveryTag, false);
                    }
                };

                // Configurar las colas a consumir
                SetupQueues();

                // Iniciar consumo
                _channel.BasicConsume(
                    queue: "catalog.product.updated",
                    autoAck: false,
                    consumer: consumer);

                _channel.BasicConsume(
                    queue: "catalog.product.deleted",
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("RabbitMQ Consumer iniciado correctamente");

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
            // Declarar exchange (debe coincidir con el del publicador)
            _channel.ExchangeDeclare(
                exchange: "catalog.events",
                type: ExchangeType.Topic,
                durable: true);

            // Declarar colas para el carrito
            _channel.QueueDeclare(
                queue: "catalog.product.updated",
                durable: true,
                exclusive: false,
                autoDelete: false);

            _channel.QueueDeclare(
                queue: "catalog.product.deleted",
                durable: true,
                exclusive: false,
                autoDelete: false);

            // Bindear colas al exchange con routing keys
            _channel.QueueBind(
                queue: "catalog.product.updated",
                exchange: "catalog.events",
                routingKey: "catalog.product.updated");

            _channel.QueueBind(
                queue: "catalog.product.deleted",
                exchange: "catalog.events",
                routingKey: "catalog.product.deleted");

            _logger.LogInformation("Colas de RabbitMQ configuradas correctamente");
        }

        private async Task ProcessMessage(string message, string routingKey)
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                switch (routingKey)
                {
                    case "catalog.product.updated":
                        _logger.LogInformation($"Procesando actualización de producto: {message}");
                        // Aquí puedes deserializar el mensaje y actualizar datos en Cart
                        var priceConsumer = scope.ServiceProvider.GetRequiredService<ProductPriceChangedConsumer>();
                        await priceConsumer.HandleAsync(message);
                        break;

                    case "catalog.product.deleted":
                        _logger.LogInformation($"Procesando eliminación de producto: {message}");
                        // Aquí puedes deserializar el mensaje y eliminar del carrito
                        // var productEvent = JsonSerializer.Deserialize<ProductDeletedEvent>(message);
                        // await RemoveProductFromCarts(productEvent);
                        break;

                    default:
                        _logger.LogWarning($"Routing key no reconocido: {routingKey}");
                        break;
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error procesando mensaje con routing key: {routingKey}");
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

                _logger.LogInformation("Conexión RabbitMQ cerrada correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cerrando conexión RabbitMQ");
            }

            base.Dispose();
        }
    }
}