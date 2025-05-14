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
Console.WriteLine(grpcPort);
Console.WriteLine(restPort);
// 1. Configuración básica del servicio
builder.Services.AddControllers();


builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Configuración de Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UserIdentityDbContext>();  // Verifica conexión a la BD

// 3. Configuración de Swagger (solo para desarrollo)
if (builder.Environment.IsDevelopment())
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

// Opción A: Solo localhost (más seguro en desarrollo)
//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.ListenLocalhost(5003, o => o.Protocols = HttpProtocols.Http2); // gRPC
//});


builder.WebHost.ConfigureKestrel(options =>
{
    // gRPC (HTTP/2 exclusivo)
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // REST (HTTP/1.1 exclusivo)
    options.ListenAnyIP(restPort, listenOptions => 
    {
        listenOptions.Protocols = HttpProtocols.Http1;
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

// 4. Inicialización de la base de datos
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<UserIdentityDbContext>();
        await context.Database.MigrateAsync(); // Aplicar migraciones

        // 👇 Usa RoleManager<ApplicationRole> en lugar de IdentityRole
        await DbInitializer.InitializeAsync(
            context,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<ApplicationRole>>() // Tipo corregido
        );
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error inicializando BD en desarrollo");
    }
}

// 5. Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UsuarioService v1");
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.UseMiddleware<ExceptionMiddleware>();
// 6. Endpoint de Health Check (disponible siempre)
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

app.Run();