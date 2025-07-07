using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Cart.Infrastructure.Persistence;

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