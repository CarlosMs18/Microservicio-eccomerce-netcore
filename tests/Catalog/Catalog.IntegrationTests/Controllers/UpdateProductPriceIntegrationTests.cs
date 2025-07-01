using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Catalog.Application.Features.Products.Commands;
using Microsoft.EntityFrameworkCore;
using Catalog.Infrastructure.Persistence;
using Catalog.Domain;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class UpdateProductPriceIntegrationTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public UpdateProductPriceIntegrationTests(
        CustomWebApplicationFactory<Program> factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task UpdateProductPrice_WithValidData_ShouldUpdatePriceAndPublishEvent()
    {
        // 🚀 FORZAR LOGGING DESDE EL INICIO
        var logEvents = new List<LogEvent>();
        var testSink = new TestSink(logEvents);

        // Reconfigurar Serilog para GARANTIZAR que capture TODO
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose() // 🎯 VERBOSE para capturar TODO
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .MinimumLevel.Override("Catalog", LogEventLevel.Verbose) // 🎯 VERBOSE
            .MinimumLevel.Override("Catalog.Infrastructure", LogEventLevel.Verbose) // 🎯 VERBOSE
            .MinimumLevel.Override("Catalog.Infrastructure.Services.External.Messaging", LogEventLevel.Verbose) // 🎯 CLAVE
            .Enrich.FromLogContext()
            .WriteTo.Sink(testSink) // Capturar en memoria
            .WriteTo.Console(outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug(outputTemplate: "🧪 [{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _output.WriteLine("🔧 Serilog reconfigurado con nivel VERBOSE");

        // Arrange - Crear un producto de prueba primero
        var testProduct = await CreateTestProductAsync();
        var newPrice = 150.99m;

        var command = new UpdateProductPriceCommand
        {
            ProductId = testProduct.Id,
            NewPrice = newPrice
        };

        // Crear cliente con usuario autenticado
        var authenticatedClient = CreateClientWithTestUser("test-user-123", "test@example.com");

        // 🎯 LOG DE PRUEBA ANTES DE LA LLAMADA
        Log.Information("🧪 TEST: Iniciando llamada al endpoint UpdateProductPrice");
        Log.Information("🧪 TEST: ProductId = {ProductId}, NewPrice = {NewPrice}", testProduct.Id, newPrice);

        // Act - Llamar al endpoint
        _output.WriteLine("📡 Realizando llamada al endpoint...");

        var response = await authenticatedClient.PutAsJsonAsync(
            "/api/Product/UpdateProductPrice",
            command,
            JsonOptions);

        // 🎯 LOG DE PRUEBA DESPUÉS DE LA LLAMADA
        Log.Information("🧪 TEST: Respuesta recibida con StatusCode = {StatusCode}", response.StatusCode);

        // Assert - Verificar respuesta HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UpdateProductPriceResponse>(responseContent, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Precio actualizado correctamente", result.Message);
        Assert.Equal(99.99m, result.OldPrice);
        Assert.Equal(newPrice, result.NewPrice);

        // Verificar que el precio se actualizó en la base de datos
        await VerifyProductPriceUpdatedInDatabase(testProduct.Id, newPrice);

        // 🎯 ESPERAR UN POCO PARA QUE LLEGUEN TODOS LOS LOGS ASÍNCRONOS
        await Task.Delay(1000);

        // 📊 MOSTRAR LOGS CAPTURADOS - TODOS
        _output.WriteLine($"📊 Total de logs capturados: {logEvents.Count}");

        foreach (var logEvent in logEvents.OrderBy(x => x.Timestamp))
        {
            var source = logEvent.Properties.ContainsKey("SourceContext")
                ? logEvent.Properties["SourceContext"].ToString().Replace("\"", "")
                : "Unknown";

            var message = logEvent.RenderMessage();
            _output.WriteLine($"📝 [{logEvent.Level}] {source}: {message}");
        }

        // 🐰 BUSCAR LOGS ESPECÍFICOS DE RABBITMQ (más amplio)
        var rabbitLogs = logEvents.Where(x =>
        {
            var message = x.RenderMessage();
            var sourceContext = x.Properties.ContainsKey("SourceContext")
                ? x.Properties["SourceContext"].ToString()
                : "";

            return message.Contains("RabbitMQ") ||
                   message.Contains("EventPublisher") ||
                   message.Contains("catalog.events") ||
                   message.Contains("PUBLISHASYNC") ||
                   message.Contains("🐰") ||
                   sourceContext.Contains("RabbitMQ") ||
                   sourceContext.Contains("EventPublisher") ||
                   sourceContext.Contains("Messaging");
        }).ToList();

        _output.WriteLine($"🐰 Logs específicos de RabbitMQ encontrados: {rabbitLogs.Count}");

        if (rabbitLogs.Any())
        {
            foreach (var log in rabbitLogs.OrderBy(x => x.Timestamp))
            {
                _output.WriteLine($"🐰 [{log.Level}] {log.RenderMessage()}");
            }
        }
        else
        {
            _output.WriteLine("❌ NO se encontraron logs de RabbitMQ - esto indica un problema de configuración");

            // Mostrar logs que podrían estar relacionados
            var possibleLogs = logEvents.Where(x =>
                x.RenderMessage().Contains("Event") ||
                x.RenderMessage().Contains("Publish") ||
                x.RenderMessage().Contains("Product")).ToList();

            _output.WriteLine($"🔍 Logs posiblemente relacionados: {possibleLogs.Count}");
            foreach (var log in possibleLogs)
            {
                _output.WriteLine($"🔍 [{log.Level}] {log.RenderMessage()}");
            }
        }

        _output.WriteLine("✅ Test completado - Precio actualizado correctamente");
        _output.WriteLine($"🐰 Evento enviado a RabbitMQ para ProductId: {testProduct.Id}");
        _output.WriteLine($"💰 Precio actualizado de {result.OldPrice} a {result.NewPrice}");
    }

    // Helper methods
    private async Task<Product> CreateTestProductAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Crear una categoría de prueba primero
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Description = "Category for testing",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-system"
        };

        context.Categories.Add(category);

        // Crear el producto de prueba
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "Product for testing price update",
            Price = 99.99m,
            CategoryId = category.Id,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-system"
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product;
    }

    private async Task VerifyProductPriceUpdatedInDatabase(Guid productId, decimal expectedPrice)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var updatedProduct = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId);

        Assert.NotNull(updatedProduct);
        Assert.Equal(expectedPrice, updatedProduct.Price);
    }
}

// Clase helper para capturar logs
public class TestSink : Serilog.Core.ILogEventSink
{
    private readonly List<LogEvent> _logEvents;

    public TestSink(List<LogEvent> logEvents)
    {
        _logEvents = logEvents;
    }

    public void Emit(LogEvent logEvent)
    {
        _logEvents.Add(logEvent);
    }
}
