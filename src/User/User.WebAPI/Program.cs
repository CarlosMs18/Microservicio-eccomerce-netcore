using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using User.Application.Models;
using User.Infrastructure;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("🚀 Iniciando User Service");

    var builder = WebApplication.CreateBuilder(args);

    // 1. Detección de entorno
    var environment = DetectEnvironment();
    Log.Information("🔧 Entorno detectado: {Environment}", environment);

    // 2. Carga de configuración
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddEnvironmentVariables();

    // 3. Configuración de Serilog
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

    // 4. Configuración de puertos
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest", 7251);
    var grpcPort = portsConfig.GetValue<int>("Grpc", 5003);

    // 5. Registro de servicios
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    // 6. Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<UserIdentityDbContext>(
            name: "sqlserver",
            tags: new[] { "db", "sqlserver" })
        .AddCheck("service_status", () =>
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Service is responsive"),
            tags: new[] { "service" });

    // 7. Configuración de Swagger solo para desarrollo
    if (environment == "Development")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "User API", Version = "v1" });
        });
    }

    // 8. Configuración de Kestrel (REST + gRPC)
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

    var app = builder.Build();

    // 9. Middleware pipeline
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
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    // 10. Health Check endpoint
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

    // 11. gRPC Service
    app.MapGrpcService<AuthGrpcService>();

    // 12. Migraciones de BD
    if (environment is "Development" or "Docker" or "Kubernetes")
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var retryCount = 0;
        const int maxRetries = 10;

        while (retryCount < maxRetries)
        {
            try
            {
                var context = services.GetRequiredService<UserIdentityDbContext>();

                Log.Information("🔄 Creando/migrando base de datos...");
                await context.Database.MigrateAsync();

                Log.Information("📊 Inicializando datos...");
                await DbInitializer.InitializeAsync(
                    context,
                    services.GetRequiredService<UserManager<ApplicationUser>>(),
                    services.GetRequiredService<RoleManager<ApplicationRole>>()
                );

                Log.Information("🆗 Base de datos lista");
                Log.Information("👥 Usuarios: {UserCount}, Roles: {RoleCount}",
                    context.Users.Count(),
                    context.Roles.Count());
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                Log.Warning(ex, "❌ Intento {Retry}/{MaxRetries} - Error: {Message}",
                    retryCount, maxRetries, ex.Message);

                if (retryCount >= maxRetries)
                {
                    Log.Fatal(ex, "❌ Error crítico con BD después de {MaxRetries} intentos", maxRetries);
                    throw;
                }

                var delaySeconds = 5 * retryCount;
                Log.Information("⏳ Reintentando en {Delay} segundos...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    // 13. Log de endpoints configurados
    LogEndpointsConfiguration(builder.Configuration, environment, restPort, grpcPort);

    Log.Information("✅ User Service listo y ejecutándose");
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
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        return "Kubernetes";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
        return "Docker";
    return "Development";
}

static void LogEndpointsConfiguration(IConfiguration config, string environment, int restPort, int grpcPort)
{
    try
    {
        var connectionParams = config.GetSection("ConnectionParameters");
        var databaseName = config["User:DatabaseName"] ?? "UserDB_Dev";
        var serverName = connectionParams["server"] ?? "Unknown";

        Log.Information("🗃️ DB para {Environment}: {Database} en {Server}", environment, databaseName, serverName);

        Log.Information("🌐 Endpoints configurados:");
        Log.Information("  REST API: http://localhost:{RestPort}/api/User/", restPort);
        Log.Information("  gRPC Service: http://localhost:{GrpcPort}", grpcPort);
        Log.Information("  Health Check: http://localhost:{RestPort}/health", restPort);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Error al mostrar configuración de endpoints");
    }
}