using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using User.Application.Models;
using User.Infrastructure;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.External.Grpc;
using User.WebAPI.Middlewares;
using Users.Application;

var builder = WebApplication.CreateBuilder(args);
var grpcPort = builder.Configuration.GetValue<int>("Grpc:Port");
var restPort = builder.Configuration.GetValue<int>("RestPort");
var isProduction = builder.Environment.IsProduction();

// 1. Configuración básica del servicio
builder.Services.AddControllers();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UserIdentityDbContext>();

// 3. Swagger (solo desarrollo)
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

// 4. Configuración de Kestrel (HTTP/HTTPS condicional)
builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC (HTTP/2 siempre)
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // REST (HTTP/1.1 + HTTPS según entorno)
    options.ListenAnyIP(restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;

        if (isProduction)
        {
            // En producción: Certificado real montado en /app/certs/
            listenOptions.UseHttps("/app/certs/tls.crt", "/app/certs/tls.key");
        }
        else
        {
            // En desarrollo: Certificado autofirmado
            listenOptions.UseHttps();
        }
    });
});

var app = builder.Build();

// 5. Inicialización de la BD (solo desarrollo)
if (!isProduction)
{
    await InitializeDatabase(app);
}

// 6. Pipeline HTTP
if (!isProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UsuarioService v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<ExceptionMiddleware>();

// 7. Health Check
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

// Método de inicialización de BD
async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<UserIdentityDbContext>();
        await context.Database.MigrateAsync();

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
    }
}