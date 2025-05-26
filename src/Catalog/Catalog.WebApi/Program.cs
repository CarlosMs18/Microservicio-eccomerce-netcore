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
using Shared.Core.Interfaces;
using System.Net.Http.Headers;
using User.Auth;

var builder = WebApplication.CreateBuilder(args);

// ========== CONFIGURACIÓN DE LOGGING ==========
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole()
          .AddConfiguration(builder.Configuration.GetSection("Logging"))
          .SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Catalog.Program");

try
{
    var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    var isDocker = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
    var environmentName = isKubernetes ? "Kubernetes" : isDocker ? "Docker" : "Local";
    logger.LogInformation("====================================");
    logger.LogInformation("Iniciando Catalog Service");
    logger.LogInformation("Iniciando aplicación en modo: {Environment}", environmentName);
    logger.LogInformation("====================================");

    // ========== CONFIGURACIÓN DE SERVICIOS ==========
    var restPort = builder.Configuration.GetValue<int>("RestPort");
    logger.LogDebug("Configurando puerto REST: {RestPort}", restPort);

    // Configuración HttpClient para UserService
    builder.Services.AddHttpClient<IExternalAuthService, UserHttpService>(client =>
    {
        var baseUrl = builder.Configuration["UserMicroservice:BaseUrl"];
        var timeoutSeconds = builder.Configuration.GetValue<int>("UserMicroservice:TimeoutSeconds");

        logger.LogDebug("Configurando HttpClient para UserService - URL: {BaseUrl}, Timeout: {Timeout}s",
            baseUrl, timeoutSeconds);

        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    })
    .AddPolicyHandler(HttpClientPolicies.GetRetryPolicy(builder.Configuration))
    .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy(builder.Configuration));

    // Configuración gRPC Client
    var grpcUrl = builder.Configuration["Grpc:UserUrl"]!;
    logger.LogDebug("Configurando gRPC Client para: {GrpcUrl}", grpcUrl);

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

    builder.Services.AddSingleton(provider =>
        GrpcChannel.ForAddress(
            grpcUrl,
            new GrpcChannelOptions
            {
                HttpHandler = provider.GetRequiredService<SocketsHttpHandler>()
            }));

    // Configuración de la base de datos
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
    var connectionString = string.Format(
        builder.Configuration.GetConnectionString("CatalogConnection"),
        dbPassword
    );

    // Log sanitizado de la conexión
    logger.LogInformation("Configuración de base de datos:");
    logger.LogInformation("• Servidor: {Server}", "sql-service.dev.svc.cluster.local");
    logger.LogInformation("• Base de datos: {Database}", "CatalogDB_Dev");
    logger.LogInformation("• Timeout: {Timeout}s", 30);

    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseSqlServer(connectionString,
            sqlOptions => sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName)));

    // Configuración básica de la aplicación
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Configuración de Swagger
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new()
            {
                Title = "Catalog Service API",
                Version = "v1",
                Description = "Microservicio para gestión de catálogo de productos"
            });
        });
    }

    // Configuración de Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(restPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            logger.LogDebug("Endpoint HTTP/1.1 configurado en puerto {Port}", restPort);
        });
    });

    var app = builder.Build();

    // ========== CONFIGURACIÓN DE MIDDLEWARE ==========
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog Service v1");
            c.DisplayRequestDuration();
        });
        logger.LogInformation("Swagger habilitado en /swagger");
    }

    // Middleware pipeline
    app.UseHttpsRedirection();
    app.UseRouting();
   // app.UseMiddleware<TokenGrpcValidationMiddleware>();
   app.UseMiddleware<TokenValidationMiddleware>();
   
    app.UseAuthorization();
    app.MapControllers();

    // Endpoint para protobuf en desarrollo
    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/proto/auth.proto", async context =>
        {
            await context.Response.WriteAsync(
                await File.ReadAllTextAsync("../User/User.Infrastructure/Protos/auth.proto"));
        });
    }

    // Middleware de manejo de excepciones
    app.UseMiddleware<ExceptionMiddleware>();


    // ========== INICIALIZACIÓN DE BD ==========
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            logger.LogInformation("Aplicando migraciones de BD...");
            var context = services.GetRequiredService<CatalogDbContext>();
            await context.Database.MigrateAsync();
            await CatalogDbInitializer.InitializeAsync(context);
            logger.LogInformation("Migraciones aplicadas correctamente");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error aplicando migraciones");
            throw;
        }
    }

    logger.LogInformation("Catalog Service iniciado correctamente");
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Error crítico durante el inicio del servicio");
    throw;
}