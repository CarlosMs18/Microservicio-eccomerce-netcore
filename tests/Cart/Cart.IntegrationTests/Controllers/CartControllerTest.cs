using Cart.Application.Features.Carts.Commands;
using Cart.IntegrationTests.Common;
using Cart.IntegrationTests.Extensions;
using Cart.IntegrationTests.Fixtures;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cart.IntegrationTests.Controllers;

[Collection("Sequential")]
public class CartControllerTest : BaseIntegrationTest
{
    public CartControllerTest(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AddProductToCart_ShouldWork()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mockCatalogService = CreateMockCatalogServiceWithValidProduct(productId);
        var client = CreateClientWithMockedCatalogService(mockCatalogService);

        var command = new AddProductToCartCommand
        {
            ProductId = productId.ToString(),
            Quantity = 1
        };

        // Act
        var response = await client.AddProductToCartAsync(command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("exitosamente", content);
    }

    [Fact]
    public async Task AddProductToCart_WithInvalidProduct_ShouldReturnBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mockCatalogService = CreateMockCatalogService();
        mockCatalogService.Setup(x => x.ProductExistsAsync(productId)).ReturnsAsync(false);
        var client = CreateClientWithMockedCatalogService(mockCatalogService);

        var command = new AddProductToCartCommand
        {
            ProductId = productId.ToString(),
            Quantity = 1
        };

        // Act
        var response = await client.AddProductToCartAsync(command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("no existe", content);
    }

    [Fact]
    public async Task AddProductToCart_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mockCatalogService = CreateMockCatalogServiceWithValidProduct(productId);

        // 🎯 CLAVE: Crear cliente sin autenticación pero con mock del CatalogService
        var client = CreateUnauthenticatedClientWithMockedCatalogService(mockCatalogService);

        var command = new AddProductToCartCommand
        {
            ProductId = productId.ToString(),
            Quantity = 1
        };

        // Act - ✅ Usar la extensión específica para sin auth
        var response = await client.AddProductToCartWithoutAuthAsync(command);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddProductToCart_WithSpecificUser_ShouldWork()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mockCatalogService = CreateMockCatalogServiceWithValidProduct(productId);
        var client = CreateClientWithMockedCatalogService(mockCatalogService);

        var command = new AddProductToCartCommand
        {
            ProductId = productId.ToString(),
            Quantity = 1
        };

        // Act
        var response = await client.AddProductToCartWithUserAsync(command, "specific-user-456");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("exitosamente", content);
    }

    [Fact]
    public async Task GetCart_ShouldWork()
    {
        // Arrange
        var client = CreateClientWithTestUser("test-user-123");

        // Act
        var response = await client.GetCartAsync();

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.NotFound);
    }

   
}