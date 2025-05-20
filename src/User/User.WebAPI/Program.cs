using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using User.Application.Models;
using User.Infrastructure;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;

var builder = WebApplication.CreateBuilder(args);

// 0. Detección automática de entorno
var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
var isProduction = builder.Environment.IsProduction();

// 1. Configuración de puertos
var grpcPort = isKubernetes
    ? int.Parse(Environment.GetEnvironmentVariable("GRPC_PORT") ?? "5001")
    : builder.Configuration.GetValue<int>("Grpc:Port");

var restPort = isKubernetes
    ? int.Parse(Environment.GetEnvironmentVariable("REST_PORT") ?? "80")
    : builder.Configuration.GetValue<int>("RestPort");





// 2. Configuración básica del servicio
builder.Services.AddControllers();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UserIdentityDbContext>();

// 4. Swagger (solo desarrollo)
if (!isProduction)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Microservicio de Usuarios",
            Version = "v1",
            Description = "API para gestión de usuarios y autenticación"
        });
    });
}

// 5. Configuración de Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC (HTTP/2 siempre)
    options.ListenAnyIP(grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);

    // REST (HTTP/1.1)
    options.ListenAnyIP(restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
        if (isProduction && !isKubernetes) listenOptions.UseHttps("/app/certs/tls.crt", "/app/certs/tls.key");
    });
});

var app = builder.Build();

// 6. Inicialización de la BD (solo desarrollo/K8s)
if (!isProduction || isKubernetes)
{
    try
    {
        await InitializeDatabase(app);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error inicializando BD");
        if (isProduction) throw;
    }
}

// 7. Pipeline HTTP
if (!isProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UsuarioService v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<ExceptionMiddleware>();

// 8. Health Check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = new
        {
            estado = report.Status.ToString(),
            componentes = report.Entries.Select(e => new
            {
                nombre = e.Key,
                estado = e.Value.Status.ToString(),
                detalles = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapGrpcService<AuthGrpcService>();
await app.RunAsync();


var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var connectionString = string.Format(
    builder.Configuration.GetConnectionString("IdentityConnectionString"),
    dbPassword
);
Console.WriteLine("Cadena de conexión: {0}",
    connectionString.Replace(dbPassword, "*****")); // Sanitizado
// Método de inicialización de BD (3 parámetros)
async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<UserIdentityDbContext>();
        await context.Database.MigrateAsync();

        // Versión con 3 parámetros (original)
        await DbInitializer.InitializeAsync(
            context,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<ApplicationRole>>()
        );
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error inicializando BD");
        throw;
    }
}