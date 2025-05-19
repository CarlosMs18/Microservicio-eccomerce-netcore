using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Resilience;
using Catalog.Infrastructure.SyncDataServices.Grpc;
using Catalog.Infrastructure.SyncDataServices.Http;
using Catalog.WebAPI.Middlewares;
using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Shared.Core.Interfaces;
using System.Net.Http.Headers;
using User.Auth;

var builder = WebApplication.CreateBuilder(args);
var restPort = builder.Configuration.GetValue<int>("RestPort");

builder.Services.AddHttpClient<IExternalAuthService, UserHttpService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["UserMicroservice:BaseUrl"]);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("UserMicroservice:TimeoutSeconds")
    );
})
.AddPolicyHandler(HttpClientPolicies.GetRetryPolicy(builder.Configuration))
.AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy(builder.Configuration));

builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["Grpc:UserUrl"]);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
})
.AddPolicyHandler(Policy<HttpResponseMessage>
    .Handle<RpcException>(e => e.StatusCode == StatusCode.Unavailable)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
);


builder.Services.AddSingleton<UserGrpcClient>();


builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}


builder.WebHost.ConfigureKestrel(options =>
{
    // Endpoint para REST (HTTP/1.1)
    options.ListenAnyIP(restPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    // Opcional: Endpoint para gRPC (HTTP/2) - Solo si Catalog sirve gRPC
    // options.ListenAnyIP(grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<CatalogDbContext>();
        await context.Database.MigrateAsync(); // Aplica migraciones
        await CatalogDbInitializer.InitializeAsync(context); // Seeding
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error inicializando la BD de catálogo");
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseRouting();
/*app.UseMiddleware<TokenValidationMiddleware>();*/ /*comunicacion HTTP*/

app.UseMiddleware<TokenGrpcValidationMiddleware>(); /*comunicacion GRPC*/
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/proto/auth.proto", async context =>
    {
        await context.Response.WriteAsync(
            await File.ReadAllTextAsync("../User/User.Infrastructure/Protos/auth.proto"));
    });
}

app.UseMiddleware<ExceptionMiddleware>();

app.Run();