using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Shared.Infrastructure.Interfaces;
using User.Infrastructure;
using User.Infrastructure.Extensions;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.Infrastructure.Services.Internal;
using User.WebAPI.Middlewares;
using Users.Application;

// Test comment to trigger workflow v2
// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("👤 Iniciando User Service!");
    

    var builder = WebApplication.CreateBuilder(args);

    // 1. Configuración básica
    var environment = DetectEnvironment();

    ConfigureAppSettings(builder, environment);
    ConfigureSerilog(builder, environment);

    // 2. Servicios
    var (restPort, grpcPort) = ConfigureServices(builder, environment);

    // 3. App pipeline
    var app = builder.Build();
    ConfigureMiddleware(app, environment);

    // 4. Inicialización
    await app.Services.EnsureDatabaseAsync(environment);

    builder.Configuration.LogEndpointsConfiguration(environment, restPort, grpcPort);

    Log.Information("✅ User Service listo y ejecutándose.");
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
        return "CI";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        return "Kubernetes";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
        return "Docker";
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

static (int restPort, int grpcPort) ConfigureServices(WebApplicationBuilder builder, string environment)
{
    // Configuración de puertos
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest");
    var grpcPort = portsConfig.GetValue<int>("Grpc");

    // Servicios básicos
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();

    // Servicios de aplicación e infraestructura
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

   
    // Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<UserIdentityDbContext>(
            name: "sqlserver",
            tags: new[] { "db", "sqlserver" })
        .AddCheck("service_status", () =>
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Service is responsive"),
            tags: new[] { "service" });

    // Swagger solo para desarrollo
    if (environment == "Development")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "User API", Version = "v1" });
        });
    }

    // Configuración de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
        options.Limits.MinRequestBodyDataRate = null;

        // REST Endpoint
        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            Log.Debug("🌐 HTTP REST configurado en puerto {Port}", restPort);
        });

        // gRPC Endpoint
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
    if (environment == "Development")
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "User v1");
            c.DisplayRequestDuration();
        });
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    // 🔥 ORDEN CORRECTO: UseHttpMetrics() después de UseRouting()
    app.UseHttpMetrics();
    app.UseAuthorization();

    // 🔥 ORDEN CORRECTO: MapMetrics() después de UseRouting()
    app.MapMetrics();
    app.MapControllers();

    // Middleware personalizado debe ir después de MapMetrics()
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    // Health Check endpoint
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            var result = new
            {
                status = report.Status.ToString(),
                environment = environment,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message
                })
            };
            await context.Response.WriteAsJsonAsync(result);
        }
    });

    // gRPC Service
    app.MapGrpcService<AuthGrpcService>();

}

public partial class Program { }