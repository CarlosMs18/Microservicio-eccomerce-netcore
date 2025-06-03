using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Services.External.Grpc;
using Catalog.WebAPI.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("🚀 Iniciando Catalog Service");

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

    // 4. Configuración de puerto REST
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest", 7204);
    var grpcPort = portsConfig.GetValue<int>("Grpc", 7205);

    // 5. Registro de servicios (toda la configuración técnica ahora está en Infrastructure)
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    // 6. Configuración de Swagger solo para desarrollo
    if (environment == "Development")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Catalog API", Version = "v1" });
        });
    }

    // 7. Configuración de Kestrel (solo HTTP REST)
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

    var app = builder.Build();

    // 8. Middleware pipeline
    if (environment == "Development")
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
    app.UseMiddleware<TokenGrpcValidationMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    // Mapear servicio gRPC
    app.MapGrpcService<CatalogGrpcService>();

    // 9. Migraciones de BD
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
                var db = services.GetRequiredService<CatalogDbContext>();

                Log.Information("🔄 Creando/migrando base de datos...");
                await db.Database.MigrateAsync();

                Log.Information("📊 Inicializando datos...");
                await CatalogDbInitializer.InitializeAsync(db);

                Log.Information("🆗 Base de datos lista");
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

    // 10. Log de endpoints configurados
    LogEndpointsConfiguration(builder.Configuration, environment, restPort);

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
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        return "Kubernetes";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
        return "Docker";
    return "Development";
}

static void LogEndpointsConfiguration(IConfiguration config, string environment, int restPort)
{
    try
    {
        var connectionParams = config.GetSection("ConnectionParameters");
        var databaseName = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev";
        var serverName = connectionParams["server"] ?? "Unknown";

        Log.Information("🗃️ DB para {Environment}: {Database} en {Server}", environment, databaseName, serverName);

        Log.Information("🌐 Endpoints configurados:");
        Log.Information("  REST API: http://localhost:{Port}/api/v1/", restPort);

        // Log de configuración gRPC (leído desde configuración)
        var microservicesConfig = config.GetSection("Microservices:User");
        var serviceParams = config.GetSection("ServiceParameters");

        var httpTemplate = microservicesConfig["HttpTemplate"] ?? "http://{host}/api/User/";
        var grpcTemplate = microservicesConfig["GrpcTemplate"] ?? "http://{host}:{port}";
        var host = serviceParams["host"] ?? "localhost";
        var port = serviceParams["port"] ?? "5001";

        var userServiceBaseUrl = httpTemplate.Replace("{host}", host);
        var grpcUrl = grpcTemplate.Replace("{host}", host).Replace("{port}", port);

        Log.Information("  User Service HTTP: {UserHttpUrl}", userServiceBaseUrl);
        Log.Information("  User Service gRPC: {UserGrpcUrl}", grpcUrl);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Error al mostrar configuración de endpoints");
    }
}