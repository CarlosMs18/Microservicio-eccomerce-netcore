using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Polly;
using System.Data.SqlClient;
using User.Application.Models;
using User.Infrastructure;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;

var builder = WebApplication.CreateBuilder(args);

// ========== CONFIGURACIÓN MULTIFUENTE ==========
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables(); // Para Docker/K8s

// ========== LOGGING AVANZADO ==========
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole()
          .AddConfiguration(builder.Configuration.GetSection("Logging"))
          .AddJsonConsole() // Mejor formato para logs estructurados
          .SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Bootstrap");

try
{
    // ========== DETECCIÓN DE ENTORNO MEJORADA ==========
    var environmentInfo = new
    {
        IsKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")),
        IsDocker = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")),
        EnvironmentName = builder.Environment.EnvironmentName,
        IsProduction = builder.Environment.IsProduction()
    };

    logger.LogInformation("""
        ====================================
        Iniciando aplicación en modo: {Environment}
        • Kubernetes: {IsK8s}
        • Docker: {IsDocker}
        • Producción: {IsProd}
        ====================================
        """,
        environmentInfo.EnvironmentName,
        environmentInfo.IsKubernetes,
        environmentInfo.IsDocker,
        environmentInfo.IsProduction);

    // ========== CONFIGURACIÓN DE RESILIENCIA ==========
    builder.Services.AddHttpClient("RetryClient")
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    // ========== CONFIGURACIÓN DE SERVICIOS ==========
    builder.Services.AddControllers();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ========== HEALTH CHECKS MEJORADOS ==========
    builder.Services.AddHealthChecks()
      .AddDbContextCheck<UserIdentityDbContext>(
          name: "sqlserver",
          tags: new[] { "db", "sqlserver" })
      // Verificación básica de que el servicio está vivo
      .AddCheck("service_status", () =>
          HealthCheckResult.Healthy("Service is responsive"),
          tags: new[] { "service" });

    // ========== SWAGGER CONFIGURABLE ==========
    if (!environmentInfo.IsProduction)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = $"User Service API - {environmentInfo.EnvironmentName}",
                Version = "v1",
                Description = $"Environment: {environmentInfo.EnvironmentName}",
                Contact = new OpenApiContact
                {
                    Name = "Dev Team",
                    Email = "dev@example.com"
                }
            });

            // Configuración adicional para ambientes no productivos
            if (environmentInfo.EnvironmentName == "Development")
            {
                c.EnableAnnotations();
            }
        });
    }

    // ========== CONFIGURACIÓN AVANZADA DE KESTREL ==========
    ConfigureKestrel(builder, environmentInfo, logger);

    var app = builder.Build();

    // ========== PIPELINE CONFIGURATION ==========
    ConfigurePipeline(app, environmentInfo, logger);

    // ========== INICIALIZACIÓN ==========
    await InitializeApplication(app, logger);

    logger.LogInformation("""
        ====================================
        Aplicación iniciada correctamente
        Entorno: {Environment}
        UTC Time: {StartTime}
        ====================================
        """,
        environmentInfo.EnvironmentName,
        DateTime.UtcNow);

    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, """
        ====================================
        ERROR CRÍTICO DURANTE EL INICIO
        Mensaje: {ErrorMessage}
        ====================================
        """, ex.Message);
    throw;
}

// ========== MÉTODOS AUXILIARES ==========

void ConfigureKestrel(WebApplicationBuilder builder, dynamic environmentInfo, ILogger logger)
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
        options.Limits.MinRequestBodyDataRate = null; // Para evitar timeouts en uploads grandes

        // Configuración dinámica de puertos
        var grpcPort = environmentInfo.IsKubernetes
            ? int.Parse(Environment.GetEnvironmentVariable("GRPC_PORT") ?? "5001")
            : builder.Configuration.GetValue<int>("Grpc:Port");

        var restPort = environmentInfo.IsKubernetes
            ? int.Parse(Environment.GetEnvironmentVariable("REST_PORT") ?? "80")
            : builder.Configuration.GetValue<int>("RestPort");

        // gRPC Endpoint
        options.ListenAnyIP(grpcPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
            logger.LogDebug("gRPC endpoint configurado en puerto {Port}", grpcPort);
        });

        // REST Endpoint
        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            if (environmentInfo.IsProduction && !environmentInfo.IsKubernetes)
            {
                listenOptions.UseHttps("/app/certs/tls.crt", "/app/certs/tls.key");
                logger.LogDebug("HTTPS habilitado para REST");
            }
            logger.LogDebug("REST endpoint configurado en puerto {Port}", restPort);
        });
    });
}

void ConfigurePipeline(WebApplication app, dynamic environmentInfo, ILogger logger)
{
    // Middleware de logging de requests
    app.Use(async (context, next) =>
    {
        var startTime = DateTime.UtcNow;
        logger.LogDebug("Iniciando request: {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        await next();

        logger.LogDebug("Request completado: {Method} {Path} - {StatusCode} en {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            (DateTime.UtcNow - startTime).TotalMilliseconds);
    });

    if (!environmentInfo.IsProduction)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API");
            c.ConfigObject.DisplayRequestDuration = true;
            c.EnablePersistAuthorization();
        });
    }

    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();
    

    // Health Check mejorado
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            var result = new
            {
                status = report.Status.ToString(),
                environment = environmentInfo.EnvironmentName,
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

async Task InitializeApplication(WebApplication app, ILogger logger)
{
    // Configuración de base de datos
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
    var connectionString = string.Format(
        app.Configuration.GetConnectionString("IdentityConnectionString"),
        dbPassword
    );

    // Log seguro de conexión (sin exponer credenciales)
    var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
    logger.LogInformation("""
        Configuración de base de datos:
        • Servidor: {Server}
        • Base de datos: {Database}
        • Usuario: {User}
        • Timeout: {Timeout}s
        • Pool size: {MaxPoolSize}
        """,
        connectionBuilder.DataSource,
        connectionBuilder.InitialCatalog,
        connectionBuilder.UserID,
        connectionBuilder.ConnectTimeout,
        connectionBuilder.MaxPoolSize);

    // Inicialización condicional de BD
    if (!app.Environment.IsProduction() ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
    {
        await InitializeDatabase(app, logger);
    }
}

async Task InitializeDatabase(WebApplication app, ILogger logger)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var dbLogger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        dbLogger.LogInformation("Iniciando migración de base de datos...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var context = services.GetRequiredService<UserIdentityDbContext>();
        await context.Database.MigrateAsync();

        dbLogger.LogInformation("Migración completada en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        dbLogger.LogInformation("Inicializando datos base...");
        stopwatch.Restart();

        await DbInitializer.InitializeAsync(
            context,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<ApplicationRole>>()
        );

        dbLogger.LogInformation("""
            Inicialización completada en {ElapsedMs}ms
            • Usuarios creados: {UserCount}
            • Roles creados: {RoleCount}
            """,
            stopwatch.ElapsedMilliseconds,
            context.Users.Count(),
            context.Roles.Count());
    }
    catch (Exception ex)
    {
        dbLogger.LogError(ex, "Error durante la inicialización de BD");
        if (app.Environment.IsProduction()) throw;
    }
}