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
    var restPort = builder.Configuration.GetValue<int>("RestPort");
    var userServiceUrl = builder.Configuration["UserMicroservice:BaseUrl"];

    // Detect environment
    var environment = DetectEnvironment();
    Log.Information("🔧 Entorno detectado: {Environment}", environment);

    // Load configuration
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    // Configure Serilog
    // Configuración óptima para producción
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("System", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .WriteTo.Async(a => a.Console()) // ← ¡Importante para performance!
              .WriteTo.Async(a => a.File(
                  new CompactJsonFormatter(),
                  "logs/log-.json",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 15,
                  buffered: true)); // ← Buffering para mejor performance
    });

    // Configure HttpClient para servicio externo
    builder.Services.AddHttpClient<IExternalAuthService, UserHttpService>(client =>
    {
        var baseUrl = builder.Configuration["UserMicroservice:BaseUrl"];
        var timeout = builder.Configuration.GetValue<int>("UserMicroservice:TimeoutSeconds");

        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(timeout);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy(builder.Configuration))
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy(builder.Configuration));

    // Configurar gRPC Client
    var grpcUrl = builder.Configuration["Grpc:UserUrl"]!;
    builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
    {
        options.Address = new Uri(grpcUrl);
    })
    .ConfigureChannel(o =>
    {
        o.HttpHandler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true
        };
    })
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<RpcException>(e =>
            e.StatusCode == StatusCode.Unavailable ||
            e.StatusCode == StatusCode.DeadlineExceeded)
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    builder.Services.AddSingleton<IUserGrpcClient, UserGrpcClient>();

    // Obtener cadena de conexión en base al entorno
    var connectionString = GetConnectionString(builder.Configuration, environment);



    Log.Information("🌐 Endpoints configurados:");
    Log.Information("  REST: http://localhost:{Port}/api/v1/", restPort);
    Log.Information("  User Service HTTP: {UserHttpUrl}", userServiceUrl);
    Log.Information("  User Service gRPC: {UserGrpcUrl}", grpcUrl);

    Log.Information("🗃️ DB para {Environment}: {Database} en {Server}",
        environment,
        DbConnectionHelper.GetDatabaseName(connectionString),
        DbConnectionHelper.GetServerName(connectionString));

    // Agregar servicios
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, environment);

    if (environment == "Local")
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
            Log.Debug("🌐 HTTP configurado en puerto {Port}", restPort);
        });
    });

    var app = builder.Build();

    // Middleware
    if (environment == "Local")
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
    //app.UseMiddleware<TokenValidationMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    // Migraciones de BD
    if (environment is "Local" or "Docker")
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

// ========== MÉTODOS AUXILIARES ==========

static string DetectEnvironment()
{
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        return "Kubernetes";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
        return "Docker";
    return "Local";
}



static string GetConnectionString(IConfiguration config, string environment)
{
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

    return environment switch
    {
        "Kubernetes" => string.Format(
            config.GetConnectionString("KubernetesConnection") ?? config.GetConnectionString("CatalogConnection")!,
            dbPassword ?? throw new ArgumentNullException(nameof(dbPassword), "DB_PASSWORD requerido en Kubernetes")),
        "Docker" => config.GetConnectionString("DockerConnection") ?? config.GetConnectionString("CatalogConnection")!,
        _ => config.GetConnectionString("CatalogConnection")!
    };
}


