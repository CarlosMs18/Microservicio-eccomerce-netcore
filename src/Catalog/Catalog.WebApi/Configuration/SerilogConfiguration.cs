using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using ILogger = Serilog.ILogger;

namespace Catalog.WebAPI.Configuration;

public static class SerilogConfiguration
{
    public static void ConfigureForEnvironment(this LoggerConfiguration config, string environment)
    {
        // Configuración base común para todos los entornos
        config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext();

        // Configuración específica por entorno
        switch (environment)
        {
            case "Testing":
            case "CI":
                ConfigureForTesting(config);
                break;
            case "Development":
                ConfigureForDevelopment(config);
                break;
            case "Docker":
            case "Kubernetes":
                ConfigureForProduction(config, environment);
                break;
            default:
                ConfigureForDevelopment(config);
                break;
        }
    }

    private static void ConfigureForTesting(LoggerConfiguration config)
    {
        config
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)

            // 🎯 CONFIGURACIONES ESPECÍFICAS PARA CAPTURAR LOGS DE RABBITMQ
            .MinimumLevel.Override("Catalog", LogEventLevel.Debug)
            .MinimumLevel.Override("Catalog.Infrastructure", LogEventLevel.Debug)
            .MinimumLevel.Override("Catalog.Application", LogEventLevel.Debug)
            .MinimumLevel.Override("Catalog.WebAPI", LogEventLevel.Debug)

            // 🐰 Esta es la línea CRÍTICA para tus logs de RabbitMQ
            .MinimumLevel.Override("Catalog.Infrastructure.Services.External.Messaging", LogEventLevel.Debug)

            .WriteTo.Console(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .WriteTo.Debug(
                outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug);
    }

    private static void ConfigureForDevelopment(LoggerConfiguration config)
    {
        config
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Catalog", LogEventLevel.Debug)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug();
    }

    private static void ConfigureForProduction(LoggerConfiguration config, string environment)
    {
        config
            .WriteTo.Async(a => a.Console())
            .WriteTo.Async(a => a.File(
                new CompactJsonFormatter(),
                $"logs/{environment.ToLower()}-log-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15));
    }

    // Método para crear el logger bootstrap
    public static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
    }
}