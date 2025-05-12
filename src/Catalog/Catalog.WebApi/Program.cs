using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Resilience;
using Catalog.Infrastructure.SyncDataServices;
using Catalog.WebAPI.Middlewares;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Interfaces;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

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


builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

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
app.UseMiddleware<TokenValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
 
app.UseMiddleware<ExceptionMiddleware>();
app.Run();