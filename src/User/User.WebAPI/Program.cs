using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Data.SqlClient;
using User.Application.Models;
using User.Infrastructure;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;

var builder = WebApplication.CreateBuilder(args);

// Configuración avanzada del logger
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole()
          .AddConfiguration(builder.Configuration.GetSection("Logging"))
          .SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Program");

try
{
    // ========== DETECCIÓN DE ENTORNO ==========
    var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    var isDocker = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
    var environmentName = isKubernetes ? "Kubernetes" : isDocker ? "Docker" : "Local";
    var isProduction = builder.Environment.IsProduction();

    logger.LogInformation("====================================");
    logger.LogInformation("Iniciando aplicación en modo: {Environment}", environmentName);
    logger.LogInformation("====================================");

    // ========== VALIDACIÓN DE CONFIGURACIÓN ==========
    if (isKubernetes)
    {
        logger.LogDebug("Configuración Kubernetes detectada");
        logger.LogDebug("Variables K8s:");
        logger.LogDebug("• GRPC_PORT: {GrpcPort}", Environment.GetEnvironmentVariable("GRPC_PORT"));
        logger.LogDebug("• REST_PORT: {RestPort}", Environment.GetEnvironmentVariable("REST_PORT"));
        logger.LogDebug("• DB_PASSWORD: {DbPasswordStatus}",
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_PASSWORD")) ? "No configurada" : "Configurada");
    }
    else if (isDocker)
    {
        logger.LogDebug("Configuración Docker detectada");
        logger.LogDebug("• Archivo de configuración: appsettings.Docker.json");
    }
    else
    {
        logger.LogDebug("Configuración Local detectada");
        logger.LogDebug("• Archivo de configuración: appsettings.Development.json");
    }

    // ========== CONFIGURACIÓN DE PUERTOS ==========
    var grpcPort = isKubernetes
        ? int.Parse(Environment.GetEnvironmentVariable("GRPC_PORT") ?? "5001")
        : builder.Configuration.GetValue<int>("Grpc:Port");

    var restPort = isKubernetes
        ? int.Parse(Environment.GetEnvironmentVariable("REST_PORT") ?? "80")
        : builder.Configuration.GetValue<int>("RestPort");

    logger.LogInformation("Puertos configurados:");
    logger.LogInformation("• gRPC: {GrpcPort} (HTTP/2)", grpcPort);
    logger.LogInformation("• REST: {RestPort} (HTTP/1.1)", restPort);

    // ========== CONFIGURACIÓN DE SERVICIOS ==========
    builder.Services.AddControllers();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Health Checks con detalles
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<UserIdentityDbContext>(
            name: "sqlserver",
            tags: new[] { "db", "sqlserver" });

    // Swagger solo en desarrollo
    if (!isProduction)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Microservicio de Usuarios",
                Version = "v1",
                Description = "API para gestión de usuarios y autenticación",
                Contact = new OpenApiContact
                {
                    Name = "Equipo de Desarrollo",
                    Email = "dev@example.com"
                }
            });
        });
    }

    // Configuración avanzada de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB

        // gRPC
        options.ListenAnyIP(grpcPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
            logger.LogDebug("Endpoint gRPC configurado en puerto {Port}", grpcPort);
        });

        // REST
        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            if (isProduction && !isKubernetes)
            {
                listenOptions.UseHttps("/app/certs/tls.crt", "/app/certs/tls.key");
                logger.LogDebug("HTTPS habilitado para REST");
            }
            logger.LogDebug("Endpoint REST configurado en puerto {Port}", restPort);
        });
    });

    var app = builder.Build();

    // ========== CONFIGURACIÓN DE MIDDLEWARE ==========
    if (!isProduction)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "UsuarioService v1");
            c.DisplayRequestDuration();
        });
        logger.LogInformation("Swagger habilitado en /swagger");
    }

    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();
    app.UseMiddleware<ExceptionMiddleware>();

    // Health Check endpoint con logging
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            var result = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message
                })
            };
            await context.Response.WriteAsJsonAsync(result);

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Health check ejecutado. Estado: {Status}", report.Status);
        }
    });

    // ========== CONFIGURACIÓN gRPC ==========
    app.MapGrpcService<AuthGrpcService>();
    logger.LogDebug("Servicio gRPC 'AuthGrpcService' mapeado");

    // ========== INFORMACIÓN DE CONEXIÓN ==========
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
    var connectionString = string.Format(
        builder.Configuration.GetConnectionString("IdentityConnectionString"),
        dbPassword
    );

    // Log detallado de conexión (sin exponer contraseña)
    var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
    logger.LogInformation("Configuración de base de datos:");
    logger.LogInformation("• Servidor: {Server}", connectionStringBuilder.DataSource);
    logger.LogInformation("• Base de datos: {Database}", connectionStringBuilder.InitialCatalog);
    logger.LogInformation("• Usuario: {User}", connectionStringBuilder.UserID);
    logger.LogInformation("• Timeout: {Timeout}s", connectionStringBuilder.ConnectTimeout);
    if (isKubernetes && string.IsNullOrEmpty(dbPassword))
    {
        logger.LogError("DB_PASSWORD no configurado en Kubernetes");
        throw new ArgumentNullException(nameof(dbPassword));
    }

    // ========== INICIALIZACIÓN DE BD ==========
    if (!isProduction || isKubernetes)
    {
        try
        {
            await InitializeDatabase(app);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error crítico durante la inicialización de BD");
            if (isProduction) throw;
        }
    }

    logger.LogInformation("Aplicación iniciada correctamente en modo {Environment}", environmentName);
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Error crítico durante el inicio de la aplicación");
    throw;
}

async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Iniciando migración de base de datos...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var context = services.GetRequiredService<UserIdentityDbContext>();
        await context.Database.MigrateAsync();

        logger.LogInformation("Migración completada en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        logger.LogInformation("Inicializando datos base...");
        stopwatch.Restart();

        await DbInitializer.InitializeAsync(
            context,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<ApplicationRole>>()
        );

        logger.LogInformation("Inicialización de datos completada en {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error durante la inicialización de BD");
        throw;
    }
}