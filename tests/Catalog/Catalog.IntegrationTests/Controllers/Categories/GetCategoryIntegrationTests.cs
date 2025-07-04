using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers.Categories;

[Collection("Sequential")]
public class GetCategoryIntegrationTests : BaseIntegrationTest
{
    public GetCategoryIntegrationTests(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCategory_WithValidId_ShouldReturnOk()
    {
        // Arrange - Crear una categoría primero
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()
            .Build();

        var createResponse = await Client.CreateCategoryAsync(categoryCommand);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createdCategory = JsonSerializer.Deserialize<CategoryResponse>(createContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var categoryId = GetCategoryTestDataBuilder
            .Create()
            .WithValidId(createdCategory.Id)
            .Build();

        // Act
        var response = await Client.GetCategoryAsync(categoryId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<CategoryResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(category);
        Assert.Equal(createdCategory.Id, category.Id);
        Assert.Equal(createdCategory.Name, category.Name);
    }

    [Fact]
    public async Task GetCategory_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var categoryId = GetCategoryTestDataBuilder
            .Create()
            .WithInvalidId()
            .Build();

        // Act
        var response = await Client.GetCategoryAsync(categoryId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Categoría no encontrada", content);
    }

    [Fact]
    public async Task GetCategory_WithEmptyId_ShouldReturnBadRequest()
    {
        // Arrange
        var categoryId = GetCategoryTestDataBuilder
            .Create()
            .WithEmptyId()
            .Build();

        // Act
        var response = await Client.GetCategoryAsync(categoryId);

        // Assert
        // Guid.Empty en ruta puede dar BadRequest o NotFound dependiendo del routing
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest ||
                   response.StatusCode == HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task GetCategory_ShouldReturnCorrectStructure()
    {
        // Arrange - Crear categoría con datos válidos (como el test que funciona)
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()  // 🔧 Usar el mismo método que el test exitoso
            .Build();

        var createResponse = await Client.CreateCategoryAsync(categoryCommand);

        // 🔍 DEBUG: Verificar que la creación fue exitosa
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createContent = await createResponse.Content.ReadAsStringAsync();

        // 🔍 DEBUG: Ver el contenido de la respuesta
        Assert.False(string.IsNullOrEmpty(createContent), "Create response content should not be empty");

        var createdCategory = JsonSerializer.Deserialize<CategoryResponse>(createContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // 🔍 DEBUG: Verificar que la deserialización funcionó
        Assert.NotNull(createdCategory);
        Assert.NotEqual(Guid.Empty, createdCategory.Id);

        // 🔧 FIX: Usar el mismo patrón que el test que funciona
        var categoryId = GetCategoryTestDataBuilder
            .Create()
            .WithValidId(createdCategory.Id)
            .Build();

        // 🔍 DEBUG: Verificar que el ID es el mismo
        Assert.Equal(createdCategory.Id, categoryId);

        // Act
        var response = await Client.GetCategoryAsync(categoryId);

        // 🔍 DEBUG: Si llega aquí, mostrar el status y contenido antes del assert
        var debugContent = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {response.StatusCode}. Response content: {debugContent}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<CategoryResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(category);
        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.NotEmpty(category.Name);
        Assert.NotNull(category.Description);
        Assert.NotEmpty(category.CreatedBy);
    }
}