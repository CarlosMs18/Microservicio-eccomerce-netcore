using Cart.Application;
using Cart.Infrastructure;
using Cart.Infrastructure.Extensions;
using Cart.WebAPI.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

    Log.Information("✅ Cart Service listo y ejecutándose");
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
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
    {
        Console.WriteLine("🚀 CI ENVIRONMENT DETECTADO");
        Log.Information("🚀 Entorno CI detectado via variable CI");
        return "CI";
    }
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (env == "Testing")
    {
        Console.WriteLine("🧪 TESTING AUTH CONFIGURADO");
        Log.Information("🧪 Entorno Testing detectado via ASPNETCORE_ENVIRONMENT");
        return "Testing";
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
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddEnvironmentVariables();
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

    // Servicios básicos
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();

    // Servicios de aplicación e infraestructura
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    if (environment == "Testing" || environment == "CI")
    {
        builder.Services.AddTestingAuthentication();
        Log.Information("🧪 Testing Authentication habilitado - Usuario fake: test-user-123");
    }
    else if (environment == "Kubernetes")
    {
        // Para Kubernetes: Leer headers del Ingress
        builder.Services.AddApiGatewayAuthentication();
        Log.Information("🔐 ApiGateway Authentication habilitado para Kubernetes");
    }
    else
    {
        // Para Development/Docker: El middleware gRPC se encarga
        // No agregamos autenticación aquí porque el middleware maneja todo
        Log.Information("🔐 Autenticación será manejada por TokenGrpcValidationMiddleware");
    }

    // Swagger solo para desarrollo
    if (environment == "Development" || environment == "Testing")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Cart API", Version = "v1" });
        });
    }

    // Configuración de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
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
    // Swagger solo para desarrollo
    if (environment == "Development" || environment == "Testing")
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

    if (environment == "Testing" || environment == "CI")
    {
        app.UseAuthentication();
        Log.Information("🧪 Testing Authentication middleware habilitado");
    }
    else if (environment == "Kubernetes")
    {
        app.UseAuthentication();
        Log.Information("🔐 ApiGateway Authentication habilitado para Kubernetes");
    }
    else
    {
        // Development/Docker: Usar middleware gRPC tradicional
        app.UseMiddleware<TokenGrpcValidationMiddleware>();
        Log.Information("🔐 TokenGrpcValidationMiddleware habilitado para entorno: {Environment}", environment);
    }

    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();
}
public partial class Program { }