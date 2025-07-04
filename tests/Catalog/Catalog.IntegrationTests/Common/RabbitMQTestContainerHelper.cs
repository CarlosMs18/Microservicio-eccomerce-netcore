using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests.Common;

/// <summary>
/// Helper simple para levantar RabbitMQ TestContainer
/// </summary>
public class RabbitMQTestContainerHelper : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private RabbitMqContainer? _rabbitMqContainer;

    public RabbitMQTestContainerHelper(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Inicia el container de RabbitMQ (reemplaza tu comando docker run)
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            _output.WriteLine("🐳 Iniciando RabbitMQ TestContainer...");

            _rabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .WithPortBinding(5672, 5672) // Puerto fijo como tu comando
                .WithPortBinding(15672, 15672) // Puerto fijo para management
                .WithUsername("guest")
                .WithPassword("guest")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
                .Build();

            await _rabbitMqContainer.StartAsync();

            _output.WriteLine("✅ RabbitMQ TestContainer iniciado en puertos fijos (5672, 15672)");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error iniciando RabbitMQ TestContainer: {ex.Message}");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_rabbitMqContainer != null)
        {
            _output.WriteLine("🐳 Deteniendo RabbitMQ TestContainer...");
            await _rabbitMqContainer.StopAsync();
            await _rabbitMqContainer.DisposeAsync();
            _output.WriteLine("✅ RabbitMQ TestContainer detenido");
        }
    }
}