using Cart.Domain;
using Cart.Infrastructure.Persistence;
using Cart.Infrastructure.Services.Messaging;
using Cart.IntegrationTests.Common;
using Cart.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;
using Shared.Core.Events;
using Microsoft.EntityFrameworkCore;

namespace Cart.IntegrationTests.Consumers;

[Collection("Sequential")]
public class ProductPriceChangedConsumerTests : BaseIntegrationTest
{
    public ProductPriceChangedConsumerTests(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task HandleAsync_ValidPriceChangeEvent_ShouldUpdateCartItemsInDatabase()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = "test-user-123";
        var oldPrice = 100.00m;
        var newPrice = 150.00m;

        // 1. Crear datos de prueba en la BD
        await SeedTestData(productId, userId, oldPrice);

        // 2. Crear el mensaje del evento
        var eventMessage = CreatePriceChangeEventMessage(productId, oldPrice, newPrice);

        // 3. Obtener el consumer del DI container
        using var scope = Factory.Services.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<ProductPriceChangedConsumer>();

        // Act
        await consumer.HandleAsync(eventMessage);

        // Assert
        await VerifyPriceUpdatedInDatabase(productId, newPrice);
    }

    [Fact]
    public async Task HandleAsync_InvalidEvent_ShouldNotUpdateDatabase()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = "test-user-123";
        var oldPrice = 100.00m;

        // 1. Crear datos de prueba en la BD
        await SeedTestData(productId, userId, oldPrice);

        // 2. Crear mensaje inválido
        var invalidEventMessage = "{ invalid json }";

        // 3. Obtener el consumer del DI container
        using var scope = Factory.Services.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<ProductPriceChangedConsumer>();

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(() => consumer.HandleAsync(invalidEventMessage));
    }

    private async Task SeedTestData(Guid productId, string userId, decimal price)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();

        // Crear carrito
        var cart = new Cart.Domain.Cart
        {
            Id = Guid.NewGuid(),
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow,
            TotalAmount = price * 2, // 2 items
            Items = new List<CartItem>()
        };

        // Crear item con el producto
        var cartItem = new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = productId,
            Quantity = 2,
            Price = price, // Precio que va a cambiar
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "TEST"
        };

        cart.Items.Add(cartItem);
        context.Carts.Add(cart);
        context.CartItems.Add(cartItem);
        await context.SaveChangesAsync();
    }

    private string CreatePriceChangeEventMessage(Guid productId, decimal oldPrice, decimal newPrice)
    {
        var eventData = new ProductPriceChangedEvent
        {
            ProductId = productId,
            OldPrice = oldPrice,
            NewPrice = newPrice
        };

        return JsonSerializer.Serialize(eventData, JsonOptions);
    }

    private async Task VerifyPriceUpdatedInDatabase(Guid productId, decimal expectedNewPrice)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();

        // Buscar el item que debería haberse actualizado
        var cartItem = await context.CartItems.FirstOrDefaultAsync(ci => ci.ProductId == productId);

        Assert.NotNull(cartItem);
        Assert.Equal(expectedNewPrice, cartItem.Price);

        // Verificar que el subtotal se calculó correctamente
        var expectedSubtotal = expectedNewPrice * cartItem.Quantity;
        Assert.Equal(expectedSubtotal, cartItem.Subtotal);
    }
}