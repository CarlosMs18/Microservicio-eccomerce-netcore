using Catalog.Application.Features.Products.Commands;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class UpdateProductPriceIntegrationTests : BaseIntegrationTest
{
    public UpdateProductPriceIntegrationTests(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task UpdateProductPrice_WithValidData_ShouldReturnOk()
    {
        // Arrange - Crear un producto primero para poder actualizarlo
        var productId = await CreateTestProduct();
        var updateCommand = new UpdateProductPriceCommand
        {
            ProductId = productId,
            NewPrice = 199.99m
        };

        // 🔍 DEBUG: Ver exactamente qué se está enviando
        Console.WriteLine($"=== SENDING UPDATE COMMAND ===");
        Console.WriteLine($"ProductId: {updateCommand.ProductId}");
        Console.WriteLine($"NewPrice: {updateCommand.NewPrice}");

        // Act
        var response = await Client.PutAsJsonAsync("/api/Product/UpdateProductPrice", updateCommand);

        // 🔍 DEBUG: Ver la respuesta completa
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"=== RESPONSE ===");
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Content: {content}");

        // ⭐ NUEVO: Si es BadRequest, mostrar los detalles
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            Console.WriteLine($"❌ BadRequest Details: {content}");
            // Intentar deserializar la respuesta del handler
            try
            {
                var badRequestResponse = JsonSerializer.Deserialize<UpdateProductPriceResponse>(content, JsonOptions);
                if (badRequestResponse != null)
                {
                    Console.WriteLine($"Handler Message: {badRequestResponse.Message}");
                    Console.WriteLine($"Handler Success: {badRequestResponse.Success}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo deserializar como UpdateProductPriceResponse: {ex.Message}");
            }
        }

        // Assert - Corrección aquí
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = JsonSerializer.Deserialize<UpdateProductPriceResponse>(content, JsonOptions);
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.Equal("Precio actualizado correctamente", responseData.Message);
        Assert.Equal(100.00m, responseData.OldPrice);
        Assert.Equal(199.99m, responseData.NewPrice);
    }

    // Helper para crear un producto de prueba
    private async Task<Guid> CreateTestProduct()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // 1. Crear primero una categoría (necesaria por FK)
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Description = "Test Category Description",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-user"
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        // 2. Ahora crear el producto con la categoría real
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "hola!",
            Price = 100.00m,
            CategoryId = category.Id, // Usar la categoría que acabamos de crear
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-user"
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product.Id;
    }
}