using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Catalog.Infrastructure.Logging;

public static class SerilogConfigurator
{
    /// <summary>
    /// Configura Serilog como bootstrap logger (antes de que se construya la app)
    /// </summary>
    public static void ConfigureBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Configura Serilog para la aplicación principal usando appsettings.json
    /// </summary>
    public static void ConfigureApplicationLogger(IHostBuilder hostBuilder, string environment)
    {
        hostBuilder.UseSerilog((ctx, services, config) =>
        {
            // 🎯 USAR LA CONFIGURACIÓN DEL appsettings.json
            config.ReadFrom.Configuration(ctx.Configuration);

            // Solo agregar configuraciones adicionales específicas por ambiente
            ConfigureEnvironmentSpecificSettings(config, environment);
        });
    }

    /// <summary>
    /// Configura Serilog específicamente para testing con mayor verbosidad
    /// </summary>
    public static void ConfigureTestingLogger(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration) // Leer del appsettings.Testing.json
            .MinimumLevel.Override("Catalog.Infrastructure.Services.External.Messaging", LogEventLevel.Debug) // 🎯 Forzar logging de RabbitMQ
            .WriteTo.Console(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .WriteTo.Debug(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();
    }

    /// <summary>
    /// Configura el sistema de logging de .NET para usar Serilog
    /// </summary>
    public static void ConfigureNetLogging(IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger, dispose: false);
            logging.SetMinimumLevel(LogLevel.Debug);
        });
    }

    private static void ConfigureEnvironmentSpecificSettings(LoggerConfiguration config, string environment)
    {
        switch (environment)
        {
            case "Testing":
                // Para testing, ya se configura en ConfigureTestingLogger
                break;

            case "Development":
                // Configuración adicional para desarrollo si es necesaria
                config.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information);
                break;

            case "Production":
            case "Kubernetes":
                // Configuración adicional para producción
                config.WriteTo.File(
                    new CompactJsonFormatter(),
                    $"logs/{environment.ToLower()}-log-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 15);
                break;
        }
    }
}