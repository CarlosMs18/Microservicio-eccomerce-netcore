using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Logging; // 🆕 NUEVA REFERENCIA
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Catalog.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. Forzar el ambiente Testing
        var environment = DetectEnvironment();
        builder.UseEnvironment(environment);

        // 📋 Configurar archivos de configuración
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .AddJsonFile($"appsettings.{environment}.json", optional: true)
                  .AddEnvironmentVariables();
        });

        // 3. 🎯 CONFIGURACIÓN DE SERILOG SIMPLIFICADA PARA TESTING
        builder.ConfigureServices((context, services) =>
        {
            // Configurar Serilog usando la clase centralizada
            SerilogConfigurator.ConfigureTestingLogger(context.Configuration);

            // Configurar el sistema de logging de .NET
            SerilogConfigurator.ConfigureNetLogging(services);

            // Log de confirmación
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<CustomWebApplicationFactory<TProgram>>>();
            logger?.LogInformation("🔧 CustomWebApplicationFactory configurado con logging DEBUG");
        });

        // 4. Usar Serilog configurado
        builder.UseSerilog(Log.Logger, dispose: false);
    }

    // 🧹 MÉTODO para limpiar la base de datos
    public async Task CleanDatabaseAsync()
    {
        Log.Information("🧹 Iniciando limpieza de base de datos para testing");
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        try
        {
            await context.ProductImages.ExecuteDeleteAsync();
            await context.Products.ExecuteDeleteAsync();
            await context.Categories.ExecuteDeleteAsync();
            await context.SaveChangesAsync();
            Log.Information("✅ Base de datos limpiada exitosamente");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error al limpiar la base de datos");
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // NO cerrar Log.Logger aquí porque otros tests pueden estar usándolo
        }
        base.Dispose(disposing);
    }

    private static string DetectEnvironment()
    {
        // Solo CI o Testing - simple y directo
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ? "CI" : "Testing";
    }
}