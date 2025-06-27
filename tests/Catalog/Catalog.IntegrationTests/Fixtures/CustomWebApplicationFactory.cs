using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Catalog.Infrastructure.Persistence;
using Catalog.Application.Contracts.Messaging;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Catalog.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Forzar el ambiente Testing
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Configurar servicios específicos para testing
        builder.ConfigureServices(services =>
        {
            // Configurar logging más silencioso para tests
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning); // Solo warnings y errores
            });

            // 🔧 MOCK del EventPublisher para tests
            var mockEventPublisher = new Mock<IEventPublisher>();
            mockEventPublisher
                .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Reemplazar el EventPublisher real con el mock
            services.AddSingleton(mockEventPublisher.Object);

            // ❌ REMOVE: No necesitamos mockear HttpContextAccessor
            // Tu TestingAuthHandler ya maneja esto correctamente

            // Tu configuración de Testing ya maneja la BD automáticamente
            // El TestingAuthHandler ya está configurado automáticamente
            // por el environment "Testing" en tu Program.cs
        });
    }

    // 🧹 MÉTODO para limpiar la base de datos
    public async Task CleanDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.ProductImages.ExecuteDeleteAsync();
        await context.Products.ExecuteDeleteAsync();
        await context.Categories.ExecuteDeleteAsync();
        await context.SaveChangesAsync();
    }
}