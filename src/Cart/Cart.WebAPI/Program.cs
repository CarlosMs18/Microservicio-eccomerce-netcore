using Cart.Application;
using Cart.Infrastructure;
using Cart.Infrastructure.Extensions;
using Cart.WebAPI.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Shared.Infrastructure.Extensions;

// Bootstrap logger!!!!12
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("🛒 Iniciando Cart Service");

    var builder = WebApplication.CreateBuilder(args);

    // 1. Configuración básica
    var environment = DetectEnvironment();
    ConfigureAppSettings(builder, environment);
    ConfigureSerilog(builder, environment);

    // 2. Servicios
    var restPort = ConfigureServices(builder, environment);

    // 3. App pipeline
    var app = builder.Build();
    ConfigureMiddleware(app, environment);

    // 4. Inicialización
    await app.Services.EnsureDatabaseAsync(environment);
    await app.Services.VerifyRabbitMQAsync(builder.Configuration, environment);

    builder.Configuration.LogEndpointsConfiguration(environment, restPort);

    Log.Information("✅ Cart Service listo y ejecutándose en entorno: {Environment}", environment);
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Error crítico al iniciar el servicio");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static string DetectEnvironment()
{
    // 🔥 PRIORIDAD: ASPNETCORE_ENVIRONMENT tiene la máxima prioridad
    var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.IsNullOrEmpty(aspnetEnv))
    {
        Log.Information("🎯 Usando ASPNETCORE_ENVIRONMENT: {Environment}", aspnetEnv);
        return aspnetEnv;
    }

    // Fallbacks para otros entornos
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
    {
        Console.WriteLine("🚀 CI ENVIRONMENT DETECTADO");
        Log.Information("🚀 Entorno CI detectado via variable CI");
        return "CI";
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
    {
        Log.Information("☸️ Entorno Kubernetes detectado");
        return "Kubernetes";
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
    {
        Log.Information("🐳 Entorno Docker detectado");
        return "Docker";
    }

    Log.Information("💻 Entorno Development detectado (por defecto)");
    return "Development";
}

static void ConfigureAppSettings(WebApplicationBuilder builder, string environment)
{
    Log.Information("🔧 Entorno detectado: {Environment}", environment);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    // 🔥 Log de configuración cargada
    Log.Information("📋 Configuraciones cargadas:");
    Log.Information("   - appsettings.json");
    Log.Information("   - appsettings.{Environment}.json", environment);
}

static void ConfigureSerilog(WebApplicationBuilder builder, string environment)
{
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("System", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .WriteTo.Async(a => a.Console())
              .WriteTo.Async(a => a.File(
                  new CompactJsonFormatter(),
                  $"logs/{environment.ToLower()}-log-.json",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 15));
    });
}

static int ConfigureServices(WebApplicationBuilder builder, string environment)
{
    // Configuración de puerto REST
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest", 5218);

    Log.Information("🚪 Configurando puerto - REST: {RestPort}", restPort);

    // Servicios básicos
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();

    // Servicios de aplicación e infraestructura
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    // 🔐 CONFIGURACIÓN DE AUTENTICACIÓN POR ENTORNO
    if (environment == "Testing" || environment == "CI")
    {
        builder.Services.AddTestingAuthentication();
        Log.Information("🧪 Testing Authentication habilitado - Usuario fake: test-user-123");
    }
    else if (environment == "Kubernetes" || environment == "Production")
    {
        builder.Services.AddApiGatewayAuthentication();
        Log.Information("🔐 ApiGateway Authentication habilitado para {Environment}", environment);
    }
    else
    {
        Log.Information("🔐 Autenticación será manejada por TokenGrpcValidationMiddleware para {Environment}", environment);
    }

    // Swagger solo para desarrollo y Kubernetes (no para Production)
    if (environment == "Development" || environment == "Kubernetes")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Cart API", Version = "v1" });
        });
        Log.Information("📖 Swagger habilitado para entorno: {Environment}", environment);
    }

    // Configuración de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Límites más estrictos en producción
        var maxBodySize = environment == "Production" ? 5 * 1024 * 1024 : 10 * 1024 * 1024; // 5MB en prod, 10MB en dev
        options.Limits.MaxRequestBodySize = maxBodySize;
        options.Limits.MinRequestBodyDataRate = null;

        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            Log.Debug("🌐 HTTP REST configurado en puerto {Port}", restPort);
        });
    });

    return restPort;
}

static void ConfigureMiddleware(WebApplication app, string environment)
{
    // Swagger solo para desarrollo y Kubernetes (no para Production)
    if (environment == "Development" || environment == "Kubernetes")
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cart v1");
            c.DisplayRequestDuration();
        });
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    // 🔥 ORDEN CORRECTO: UseHttpMetrics() después de UseRouting()
    app.UseHttpMetrics();

    // 🔐 CONFIGURACIÓN DE MIDDLEWARE DE AUTENTICACIÓN POR ENTORNO
    if (environment == "Testing" || environment == "CI")
    {
        app.UseAuthentication();
        Log.Information("🧪 Testing Authentication middleware habilitado");
    }
    else if (environment == "Kubernetes" || environment == "Production")
    {
        app.UseAuthentication();
        Log.Information("🔐 ApiGateway Authentication middleware habilitado para {Environment}", environment);
    }
    else
    {
        app.UseMiddleware<TokenGrpcValidationMiddleware>();
        Log.Information("🔐 TokenGrpcValidationMiddleware habilitado para entorno: {Environment}", environment);
    }

    app.UseAuthorization();

    // 🔥 ORDEN CORRECTO: MapMetrics() después de UseRouting()
    app.MapMetrics();
    app.MapControllers();

    // Middleware personalizado debe ir después de MapMetrics()
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();
}

public partial class Program { }