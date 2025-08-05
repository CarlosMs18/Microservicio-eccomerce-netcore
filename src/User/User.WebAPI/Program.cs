using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using User.Infrastructure;
using User.Infrastructure.Extensions;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;
using Microsoft.AspNetCore.Identity;
using User.Application.Models;

// Test comment to trigger workflow v2
// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("👤 Iniciando User Service!!!!!!!!!!!");

    var builder = WebApplication.CreateBuilder(args);

    // 1. Configuración básica
    var environment = DetectEnvironment(args); // 🔥 PASAR args como parámetro

    ConfigureAppSettings(builder, environment);
    ConfigureSerilog(builder, environment);

    // 2. Servicios
    var (restPort, grpcPort) = ConfigureServices(builder, environment);

    // 3. App pipeline
    var app = builder.Build();

    // 🔥 NUEVO: Verificar si se ejecuta con --seed-data
    if (args.Contains("--seed-data"))
    {
        Log.Information("🌱 Ejecutando seeding de datos maestros...");
        await SeedMasterDataAsync(app.Services, environment);
        Log.Information("✅ Seeding completado. Terminando aplicación.");
        return; // Terminar después del seeding, no iniciar el servidor
    }

    ConfigureMiddleware(app, environment);

    // 4. Inicialización (solo para entornos de desarrollo)
    await app.Services.EnsureDatabaseAsync(environment);

    builder.Configuration.LogEndpointsConfiguration(environment, restPort, grpcPort);

    Log.Information("✅ User Service listo y ejecutándose en entorno: {Environment}", environment);
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

// 🔥 NUEVA FUNCIÓN: Para seeding manual en producción
static async Task SeedMasterDataAsync(IServiceProvider services, string environment)
{
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;

    try
    {
        var context = scopedServices.GetRequiredService<UserIdentityDbContext>();
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedServices.GetRequiredService<RoleManager<ApplicationRole>>();

        Log.Information("🔄 Verificando conexión a base de datos...");

        // Verificar que la BD existe y está migrada
        if (!await context.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("No se puede conectar a la base de datos");
        }

        Log.Information("✅ Conexión exitosa. Ejecutando seeding...");

        if (environment == "Production")
        {
            await MasterDataSeeder.SeedAsync(context, userManager, roleManager);
        }
        else
        {
            await DbInitializer.InitializeAsync(context, userManager, roleManager);
        }

        Log.Information("🎉 Seeding ejecutado exitosamente");
        Log.Information("📊 Estado final - Usuarios: {UserCount}, Roles: {RoleCount}",
            context.Users.Count(),
            context.Roles.Count());
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "❌ Error crítico durante el seeding");
        throw;
    }
}

// 🔥 FUNCIÓN CORREGIDA: Ahora recibe args como parámetro
static string DetectEnvironment(string[] args)
{
    // 🔥 NUEVA PRIORIDAD: Argumentos de línea de comandos
    var envArg = args.FirstOrDefault(a => a.StartsWith("--environment="))?.Split('=')[1];
    if (!string.IsNullOrEmpty(envArg))
    {
        Log.Information("🎯 Usando environment desde argumentos: {Environment}", envArg);
        return envArg;
    }

    // 🔥 PRIORIDAD: ASPNETCORE_ENVIRONMENT tiene la máxima prioridad
    var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.IsNullOrEmpty(aspnetEnv))
    {
        Log.Information("🎯 Usando ASPNETCORE_ENVIRONMENT: {Environment}", aspnetEnv);
        return aspnetEnv;
    }

    // Fallbacks para otros entornos
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        return "CI";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        return "Kubernetes";
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
        return "Docker";

    return "Development";
}

// Resto de las funciones sin cambios...
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
        //var logLevel = environment == "Production" ? LogEventLevel.Warning : LogEventLevel.Information;
        var logLevel = LogEventLevel.Information;

        config.MinimumLevel.Is(logLevel)
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("System", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .WriteTo.Async(a => a.Console())
              .WriteTo.Async(a => a.File(
                  new CompactJsonFormatter(),
                  $"logs/{environment.ToLower()}-log-.json",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: environment == "Production" ? 30 : 15));
    });
}

static (int restPort, int grpcPort) ConfigureServices(WebApplicationBuilder builder, string environment)
{
    // Configuración de puertos
    var portsConfig = builder.Configuration.GetSection("Ports");
    var restPort = portsConfig.GetValue<int>("Rest");
    var grpcPort = portsConfig.GetValue<int>("Grpc");

    Log.Information("🚪 Configurando puertos - REST: {RestPort}, gRPC: {GrpcPort}", restPort, grpcPort);

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

    // Swagger solo para desarrollo y Kubernetes (no para Production)
    if (environment == "Development" || environment == "Kubernetes")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "User API", Version = "v1" });
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
    // Swagger solo para desarrollo y Kubernetes (no para Production)
    if (environment == "Development" || environment == "Kubernetes")
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