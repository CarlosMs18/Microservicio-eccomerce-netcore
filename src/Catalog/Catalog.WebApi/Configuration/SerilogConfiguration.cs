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
            case "Production":
                ConfigureForProduction(config, environment);
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
            .MinimumLevel.Warning() // 🔥 Solo warnings y errores en producción
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            // 📊 Mantener logs importantes de tu aplicación en Information
            .MinimumLevel.Override("Catalog", LogEventLevel.Information)
            .MinimumLevel.Override("Catalog.Application", LogEventLevel.Information)
            .MinimumLevel.Override("Catalog.Infrastructure", LogEventLevel.Warning)
            .MinimumLevel.Override("Catalog.WebAPI", LogEventLevel.Information)
            // 🐰 RabbitMQ en Warning para producción (solo errores importantes)
            .MinimumLevel.Override("Catalog.Infrastructure.Services.External.Messaging", LogEventLevel.Warning)
            // 📝 Console con formato limpio para Kubernetes logs
            .WriteTo.Async(a => a.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] 📦 Catalog: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information))
            // 📁 Archivos JSON estructurados para análisis posterior
            .WriteTo.Async(a => a.File(
                new CompactJsonFormatter(),
                $"logs/{environment.ToLower()}-catalog-log-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30, // 🗂️ Mantener 30 días en producción
                fileSizeLimitBytes: 100_000_000, // 🔢 100MB por archivo
                rollOnFileSizeLimit: true));
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