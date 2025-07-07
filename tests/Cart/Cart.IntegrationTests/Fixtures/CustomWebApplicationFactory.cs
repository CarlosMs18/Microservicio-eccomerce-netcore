using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Cart.Infrastructure.Persistence;
using Cart.Infrastructure.Extensions;

namespace Cart.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 🌍 Detectar entorno inteligentemente
        var environment = DetectEnvironment();

        // 1. Forzar el ambiente detectado (CI o Testing)
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

        // 2. Configurar appsettings específico para el ambiente detectado
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile($"appsettings.{environment}.json", optional: false, reloadOnChange: true);
        });

        // 3. 🎯 CONFIGURAR SERVICIOS PARA TESTING
        builder.ConfigureServices(services =>
        {
            // 🚫 SOLO REMOVER servicios RabbitMQ - NO agregar nada
            RemoveRabbitMQServices(services);
        });
    }

    private static void RemoveRabbitMQServices(IServiceCollection services)
    {
        // 🚫 Remover configuración de RabbitMQ (evita conexiones)
        var rabbitConfig = services.FirstOrDefault(s => s.ServiceType == typeof(RabbitMQConfiguration));
        if (rabbitConfig != null) services.Remove(rabbitConfig);

        // ✅ MANTENER ProductPriceChangedConsumer - ProductPriceChangedConsumerTests lo necesita
        // NO remover: services.Remove(ProductPriceChangedConsumer)

        // 🚫 🎯 REMOVER SOLO el Background Service (causa el error de conexión)
        var hostedServices = services.Where(s => s.ServiceType == typeof(IHostedService)).ToList();
        foreach (var service in hostedServices)
        {
            // Remover el servicio que trata de conectarse a RabbitMQ
            if (service.ImplementationType?.Name.Contains("RabbitMQConsumerHostedService") == true ||
                service.ImplementationType?.Name.Contains("RabbitMQ") == true)
            {
                services.Remove(service);
            }
        }
    }

    private static void AddMockRabbitMQServices(IServiceCollection services)
    {
        // 🚫 NO AGREGAMOS NADA - Solo suprimimos los servicios RabbitMQ
        // El consumer se testea por separado en ProductPriceChangedConsumerTests
        // Los endpoints REST no necesitan RabbitMQ
    }

    private static string DetectEnvironment()
    {
        // 🔍 Detección inteligente del entorno: solo CI o Testing
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ? "CI" : "Testing";
    }

    // 🧹 MÉTODO para limpiar la base de datos
    public async Task CleanDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        try
        {
            await context.CartItems.ExecuteDeleteAsync();
            await context.Carts.ExecuteDeleteAsync();
            await context.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Sin logger para evitar problemas
            throw;
        }
    }
}

// 🎯 VERSIÓN SIMPLE - Solo suprimir servicios RabbitMQ
// No necesitas el MockRabbitMQBackgroundService