using RabbitMQ.Client;
using System.Text;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests.Common;

/// <summary>
/// Helper para configurar y manejar RabbitMQ en tests de integración
/// </summary>
public class RabbitMQTestHelper
{
    private readonly ITestOutputHelper _output;

    public RabbitMQTestHelper(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Configura RabbitMQ para un test específico
    /// </summary>
    public async Task SetupRabbitMQForTestAsync(string exchangeName, string queueName, string routingKey)
    {
        try
        {
            using var connection = CreateRabbitMQConnection();
            using var channel = connection.CreateModel();

            // 🧹 CLEANUP PREVIO
            await CleanupQueueAsync(channel, queueName);

            // 🏗️ CONFIGURACIÓN FRESCA
            await SetupExchangeAsync(channel, exchangeName);
            await SetupQueueAsync(channel, queueName);
            await SetupBindingAsync(channel, queueName, exchangeName, routingKey);

            // 🧹 LIMPIAR MENSAJES
            channel.QueuePurge(queueName);

            _output.WriteLine($"✅ RabbitMQ configurado exitosamente:");
            _output.WriteLine($"   🔧 Exchange: {exchangeName}");
            _output.WriteLine($"   🔧 Queue: {queueName}");
            _output.WriteLine($"   🔧 Routing Key: {routingKey}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error configurando RabbitMQ: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene todos los mensajes de una cola específica
    /// </summary>
    public async Task<List<string>> GetMessagesFromQueueAsync(string queueName)
    {
        var messages = new List<string>();

        try
        {
            using var connection = CreateRabbitMQConnection();
            using var channel = connection.CreateModel();

            int messageCount = 0;
            while (true)
            {
                var result = channel.BasicGet(queueName, autoAck: true);
                if (result == null) break;

                var message = Encoding.UTF8.GetString(result.Body.ToArray());
                messages.Add(message);
                messageCount++;

                _output.WriteLine($"📨 Mensaje {messageCount} obtenido de la cola");

                if (messageCount > 10) break; // Protección contra bucle infinito
            }

            _output.WriteLine($"📊 Total de mensajes obtenidos: {messages.Count}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error obteniendo mensajes: {ex.Message}");
            throw;
        }

        return messages;
    }

    /// <summary>
    /// Crea una conexión a RabbitMQ con configuración de test
    /// </summary>
    public IConnection CreateRabbitMQConnection()
    {
        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(10)
        };

        try
        {
            var connection = factory.CreateConnection();
            _output.WriteLine("🔗 Conexión a RabbitMQ establecida correctamente");
            return connection;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error conectando a RabbitMQ: {ex.Message}");
            _output.WriteLine("💡 Asegúrate de que RabbitMQ esté corriendo:");
            _output.WriteLine("   docker run -d --name rabbitmq-local -p 5672:5672 -p 15672:15672 rabbitmq:3-management");
            throw;
        }
    }

    /// <summary>
    /// Limpia una cola específica
    /// </summary>
    private async Task CleanupQueueAsync(IModel channel, string queueName)
    {
        try
        {
            channel.QueueDelete(queueName, ifUnused: false, ifEmpty: false);
            _output.WriteLine($"🧹 Cola {queueName} eliminada");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Cola {queueName} no existía: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Configura el exchange
    /// </summary>
    private async Task SetupExchangeAsync(IModel channel, string exchangeName)
    {
        channel.ExchangeDeclare(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _output.WriteLine($"🏗️ Exchange {exchangeName} configurado");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Configura la cola
    /// </summary>
    private async Task SetupQueueAsync(IModel channel, string queueName)
    {
        channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _output.WriteLine($"🏗️ Cola {queueName} configurada");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Configura el binding entre cola y exchange
    /// </summary>
    private async Task SetupBindingAsync(IModel channel, string queueName, string exchangeName, string routingKey)
    {
        channel.QueueBind(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        _output.WriteLine($"🔗 Binding configurado: {queueName} -> {exchangeName} -> {routingKey}");
        await Task.CompletedTask;
    }
}