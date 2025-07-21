using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Logging; // 🆕 NUEVA REFERENCIA
using Catalog.WebAPI.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;
using Shared.Infrastructure.Extensions;

// 🎯 BOOTSTRAP LOGGER SIMPLIFICADO!!!!
//!Para el test!
SerilogConfigurator.ConfigureBootstrapLogger();

try
{
    Log.Information("🚀 Iniciando Catalog Service!!");

    var builder = WebApplication.CreateBuilder(args);

    // 1. Configuración básica
    var environment = DetectEnvironment();
    Console.WriteLine($"🔍 ENVIRONMENT: {environment}");
    ConfigureAppSettings(builder, environment);

    // 🎯 CONFIGURACIÓN DE SERILOG SIMPLIFICADA
    SerilogConfigurator.ConfigureApplicationLogger(builder.Host, environment);

    // 2. Servicios
    var (restPort, grpcPort) = ConfigureServices(builder, environment);

    // 3. App pipeline
    var app = builder.Build();
    ConfigureMiddleware(app, environment);

    // 4. Inicialización
    await app.Services.EnsureDatabaseAsync(environment);
    await app.Services.VerifyRabbitMQAsync(builder.Configuration, environment);

    builder.Configuration.LogEndpointsConfiguration(environment, restPort, grpcPort);

    Log.Information("✅ Catalog Service listo y ejecutándose");
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

static (int restPort, int grpcPort) ConfigureServices(WebApplicationBuilder builder, string environment)
{
    // Configuración de puertos
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest", 7204);
    var grpcPort = portsConfig.GetValue<int>("Grpc", 7205);

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
        builder.Services.AddApiGatewayAuthentication();
        Log.Information("🔐 ApiGateway Authentication habilitado para Kubernetes");
    }
    else
    {
        Log.Information("🔐 Autenticación será manejada por TokenGrpcValidationMiddleware");
    }

    // Swagger solo para desarrollo
    if (environment == "Development" || environment == "Testing")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Catalog API", Version = "v1" });
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

        options.ListenAnyIP(grpcPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
            Log.Debug("📡 gRPC configurado en puerto {Port}", grpcPort);
        });
    });

    return (restPort, grpcPort);
}

static void ConfigureMiddleware(WebApplication app, string environment)
{
    // Swagger solo para desarrollo
    if (environment == "Development" || environment == "Testing")
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog v1");
            c.DisplayRequestDuration();
        });
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseHttpMetrics();

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
        app.UseMiddleware<TokenGrpcValidationMiddleware>();
        Log.Information("🔐 TokenGrpcValidationMiddleware habilitado para entorno: {Environment}", environment);
    }


    app.UseAuthorization();
    app.MapMetrics();
    app.MapControllers();
    app.MapGrpcService<Catalog.Infrastructure.Services.External.Grpc.CatalogGrpcService>();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();
}

public partial class Program { }