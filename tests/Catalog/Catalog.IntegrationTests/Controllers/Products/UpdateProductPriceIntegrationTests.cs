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

namespace Catalog.IntegrationTests.Controllers.Products;

[Collection("Sequential")]
public class UpdateProductPriceIntegrationTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly RabbitMQTestHelper _rabbitMQHelper;
    private readonly RabbitMQTestContainerHelper _containerHelper;

    public UpdateProductPriceIntegrationTests(
        CustomWebApplicationFactory<Program> factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _rabbitMQHelper = new RabbitMQTestHelper(output); // Tu helper original
        _containerHelper = new RabbitMQTestContainerHelper(output); // Solo para levantar el container
    }

    // 🆕 SOLO PARA LEVANTAR Y BAJAR EL CONTAINER
    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 Levantando RabbitMQ TestContainer...");
        await _containerHelper.StartAsync();
        _output.WriteLine("✅ RabbitMQ listo - usando tu configuración existente");
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("🧹 Bajando RabbitMQ TestContainer...");
        await _containerHelper.DisposeAsync();
    }

    [Fact]
    public async Task UpdateProductPrice_WithValidData_ShouldUpdatePriceAndPublishEvent()
    {
        // Arrange
        var testProduct = await CreateTestProductAsync();
        var newPrice = 150.99m;

        var command = new UpdateProductPriceCommand
        {
            ProductId = testProduct.Id,
            NewPrice = newPrice
        };

        // 🔧 SETUP: Usar tu helper original - no cambié nada aquí
        await _rabbitMQHelper.SetupRabbitMQForTestAsync(
            TestConstants.RabbitMQ.EXCHANGE_NAME,
            TestConstants.RabbitMQ.Queues.PRODUCT_PRICE_UPDATE_TEST,
            TestConstants.RabbitMQ.RoutingKeys.PRODUCT_UPDATED);

        _output.WriteLine("🔧 RabbitMQ configurado para el test");

        // Tu código original - sin cambios
        var authenticatedClient = CreateClientWithTestUser(
            TestConstants.TestUsers.DEFAULT_USER_ID,
            TestConstants.TestUsers.DEFAULT_USER_EMAIL);

        _output.WriteLine($"📡 Iniciando test para ProductId: {testProduct.Id}");
        _output.WriteLine($"💰 Precio original: {testProduct.Price} -> Nuevo precio: {newPrice}");

        // Act - Llamar al endpoint
        var response = await authenticatedClient.PutAsJsonAsync(
            "/api/Product/UpdateProductPrice",
            command,
            JsonOptions);

        // Assert - Verificar respuesta HTTP
        await AssertHttpResponseAsync(response, newPrice);

        // Verificar que el precio se actualizó en la base de datos
        await VerifyProductPriceUpdatedInDatabase(testProduct.Id, newPrice);
        _output.WriteLine("✅ Precio actualizado en base de datos verificado");

        // 🎯 VERIFICAR MENSAJE EN RABBITMQ - tu código original
        await VerifyRabbitMQEventAsync(testProduct, newPrice);

        _output.WriteLine("🎉 Test completado exitosamente!");
    }

    /// <summary>
    /// Verifica la respuesta HTTP del endpoint
    /// </summary>
    private async Task AssertHttpResponseAsync(HttpResponseMessage response, decimal expectedNewPrice)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UpdateProductPriceResponse>(responseContent, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Precio actualizado correctamente", result.Message);
        Assert.Equal(TestConstants.TestProducts.DEFAULT_PRICE, result.OldPrice);
        Assert.Equal(expectedNewPrice, result.NewPrice);

        _output.WriteLine("✅ Respuesta HTTP validada correctamente");
    }

    /// <summary>
    /// Verifica que el evento fue publicado correctamente en RabbitMQ
    /// </summary>
    private async Task VerifyRabbitMQEventAsync(Product testProduct, decimal newPrice)
    {
        _output.WriteLine("🐰 Esperando evento en RabbitMQ...");
        await Task.Delay(TestConstants.Timeouts.EVENT_PROCESSING_DELAY_MS);

        var messages = await _rabbitMQHelper.GetMessagesFromQueueAsync(
            TestConstants.RabbitMQ.Queues.PRODUCT_PRICE_UPDATE_TEST);

        Assert.Single(messages); // Debe haber exactamente 1 mensaje
        _output.WriteLine($"🎯 Mensaje encontrado en RabbitMQ: {messages.Count}");

        // Verificar el contenido del evento
        var eventJson = messages.First();
        _output.WriteLine($"📄 JSON del evento: {eventJson}");

        var eventData = JsonSerializer.Deserialize<ProductPriceChangedEvent>(eventJson, JsonOptions);

        AssertEventData(eventData, testProduct, newPrice);
        LogEventDetails(eventData);
    }

    /// <summary>
    /// Verifica que los datos del evento sean correctos
    /// </summary>
    private static void AssertEventData(ProductPriceChangedEvent? eventData, Product testProduct, decimal newPrice)
    {
        Assert.NotNull(eventData);
        Assert.Equal(testProduct.Id, eventData.ProductId);
        Assert.Equal(TestConstants.TestProducts.DEFAULT_NAME, eventData.ProductName);
        Assert.Equal(TestConstants.TestProducts.DEFAULT_PRICE, eventData.OldPrice);
        Assert.Equal(newPrice, eventData.NewPrice);
        Assert.Equal(TestConstants.TestUsers.DEFAULT_USER_ID, eventData.ChangedBy);
    }

    /// <summary>
    /// Registra los detalles del evento en los logs de test
    /// </summary>
    private void LogEventDetails(ProductPriceChangedEvent eventData)
    {
        _output.WriteLine("✅ Contenido del evento RabbitMQ validado correctamente");
        _output.WriteLine($"   📦 ProductId: {eventData.ProductId}");
        _output.WriteLine($"   💰 Precio: {eventData.OldPrice} -> {eventData.NewPrice}");
        _output.WriteLine($"   👤 Usuario: {eventData.ChangedBy}");
        _output.WriteLine($"   🕐 Fecha: {eventData.ChangedAt}");
    }

    /// <summary>
    /// Crea un producto de prueba en la base de datos
    /// </summary>
    private async Task<Product> CreateTestProductAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Crear una categoría de prueba primero
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = TestConstants.TestCategories.DEFAULT_NAME,
            Description = TestConstants.TestCategories.DEFAULT_DESCRIPTION,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = TestConstants.TestCategories.DEFAULT_CREATOR
        };

        context.Categories.Add(category);

        // Crear el producto de prueba
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = TestConstants.TestProducts.DEFAULT_NAME,
            Description = TestConstants.TestProducts.DEFAULT_DESCRIPTION,
            Price = TestConstants.TestProducts.DEFAULT_PRICE,
            CategoryId = category.Id,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = TestConstants.TestProducts.DEFAULT_CREATOR
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        _output.WriteLine($"🏭 Producto de prueba creado: {product.Id}");
        return product;
    }

    /// <summary>
    /// Verifica que el precio del producto se actualizó correctamente en la base de datos
    /// </summary>
    private async Task VerifyProductPriceUpdatedInDatabase(Guid productId, decimal expectedPrice)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var updatedProduct = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId);

        Assert.NotNull(updatedProduct);
        Assert.Equal(expectedPrice, updatedProduct.Price);
    }

    public override void Dispose()
    {
        // El cleanup se hace en DisposeAsync()
        base.Dispose();
    }
}