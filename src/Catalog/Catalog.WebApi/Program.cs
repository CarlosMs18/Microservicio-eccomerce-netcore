using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Resilience;
using Catalog.Infrastructure.SyncDataServices.Grpc;
using Catalog.Infrastructure.SyncDataServices.Http;
using Catalog.WebAPI.Middlewares;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Shared.Core.Interfaces;
using System;
using System.Net.Http.Headers;
using User.Auth;

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

    // 4. Configuración de puertos
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest", 7204);
    var grpcPort = portsConfig.GetValue<int>("Grpc", 5003);

    // 5. Configuración de HttpClient
    var microservicesConfig = builder.Configuration.GetSection("Microservices:User");
    var serviceParams = builder.Configuration.GetSection("ServiceParameters");

    var httpTemplate = microservicesConfig["HttpTemplate"] ?? "http://{host}/api/User/";
    var host = serviceParams["host"] ?? "localhost";

    var userServiceBaseUrl = httpTemplate.Replace("{host}", host);

    builder.Services.AddHttpClient<IExternalAuthService, UserHttpService>(client =>
    {
        client.BaseAddress = new Uri(userServiceBaseUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy(builder.Configuration))
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy(builder.Configuration));

    // 6. Configuración gRPC
    var grpcTemplate = microservicesConfig["GrpcTemplate"] ?? "http://{host}:{port}";
    var grpcHost = serviceParams["host"] ?? "localhost";
    var servicePort = serviceParams["port"] ?? "5001";

    var grpcUrl = grpcTemplate
        .Replace("{host}", grpcHost)
        .Replace("{port}", servicePort);

    builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
    {
        options.Address = new Uri(grpcUrl);
    })
    .ConfigureChannel(o => o.HttpHandler = new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true
    })
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<RpcException>(e => e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    builder.Services.AddSingleton<IUserGrpcClient, UserGrpcClient>();

    // 7. Registro de servicios (sin lógica de BD aquí)
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    // 8. Configuración de Swagger solo para desarrollo
    if (environment == "Development")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Catalog API", Version = "v1" });
        });
    }

    // 9. Configuración de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            Log.Debug("🌐 HTTP configurado en puerto {Port}", restPort);
        });

        if (environment == "Development")
        {
            options.ListenAnyIP(grpcPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
                Log.Debug("🔄 gRPC configurado en puerto {Port}", grpcPort);
            });
        }
    });

    var app = builder.Build();

    // 10. Middleware pipeline
    if (environment == "Development")
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog v1");
            c.DisplayRequestDuration();
        });

        app.MapGet("/proto/auth.proto", async context =>
        {
            await context.Response.WriteAsync(await File.ReadAllTextAsync("../User/User.Infrastructure/Protos/auth.proto"));
        });
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseMiddleware<TokenGrpcValidationMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    // 11. Migraciones de BD
    if (environment is "Development" or "Docker")
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var db = services.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync();
            await CatalogDbInitializer.InitializeAsync(db);
            Log.Information("🆗 Migraciones aplicadas");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error aplicando migraciones");
            throw;
        }
    }

    // 12. Log de endpoints configurados
    LogEndpointsConfiguration(builder.Configuration, environment, restPort, userServiceBaseUrl, grpcUrl);

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

static void LogEndpointsConfiguration(IConfiguration config, string environment, int restPort, string userServiceBaseUrl, string grpcUrl)
{
    try
    {
        // Log de configuración de BD
        var connectionParams = config.GetSection("ConnectionParameters");
        var databaseName = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev";
        var serverName = connectionParams["server"] ?? "Unknown";

        Log.Information("🗃️ DB para {Environment}: {Database} en {Server}", environment, databaseName, serverName);

        // Log de endpoints
        Log.Information("🌐 Endpoints configurados:");
        Log.Information("  REST: http://localhost:{Port}/api/v1/", restPort);
        Log.Information("  User Service HTTP: {UserHttpUrl}", userServiceBaseUrl);
        Log.Information("  User Service gRPC: {UserGrpcUrl}", grpcUrl);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Error al mostrar configuración de endpoints");
    }
}