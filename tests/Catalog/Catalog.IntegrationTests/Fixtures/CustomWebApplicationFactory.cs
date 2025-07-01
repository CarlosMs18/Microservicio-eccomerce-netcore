using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Catalog.Infrastructure.Persistence;
using Catalog.Application.Contracts.Messaging;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Configuration;

namespace Catalog.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    public CustomWebApplicationFactory()
    {
        // 🚀 CONFIGURAR SERILOG EN EL CONSTRUCTOR - ANTES QUE TODO
        ConfigureSerilogForTesting();
    }

    private static void ConfigureSerilogForTesting()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .MinimumLevel.Override("Catalog", LogEventLevel.Debug)
            .MinimumLevel.Override("Catalog.Infrastructure", LogEventLevel.Debug) // 🎯 CLAVE
            .MinimumLevel.Override("Catalog.Infrastructure.Services.External.Messaging", LogEventLevel.Debug) // 🎯 MUY IMPORTANTE
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .WriteTo.Debug(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();

        Console.WriteLine("🔧 Serilog configurado para TESTING con nivel DEBUG");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. Forzar el ambiente Testing
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // 2. IMPORTANTE: Usar Serilog configurado ANTES de que se construya la app
        builder.UseSerilog(Log.Logger, dispose: false);

        // 3. Configurar appsettings específico para testing
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Testing.json", optional: false, reloadOnChange: true);

            // 🎯 FORZAR configuración de logging por código
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Serilog:MinimumLevel:Default"] = "Debug",
                ["Serilog:MinimumLevel:Override:Microsoft"] = "Information",
                ["Serilog:MinimumLevel:Override:System"] = "Information",
                ["Serilog:MinimumLevel:Override:Catalog"] = "Debug",
                ["Serilog:MinimumLevel:Override:Catalog.Infrastructure"] = "Debug",
                ["Serilog:MinimumLevel:Override:Catalog.Infrastructure.Services.External.Messaging"] = "Debug"
            });
        });

        // 4. Configurar servicios adicionales para testing
        builder.ConfigureServices(services =>
        {
            // Limpiar y reconfigurar logging
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog(Log.Logger, dispose: false);
                logging.SetMinimumLevel(LogLevel.Debug);

                // 🎯 FORZAR configuración específica para nuestras clases
                logging.AddFilter("Catalog.Infrastructure.Services.External.Messaging", LogLevel.Debug);
                logging.AddFilter("Catalog.Infrastructure.Services.External.Messaging.RabbitMQEventPublisher", LogLevel.Debug);
            });

            // Log de confirmación
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<CustomWebApplicationFactory<TProgram>>>();
            logger?.LogInformation("🔧 CustomWebApplicationFactory configurado con logging DEBUG");
        });
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
}