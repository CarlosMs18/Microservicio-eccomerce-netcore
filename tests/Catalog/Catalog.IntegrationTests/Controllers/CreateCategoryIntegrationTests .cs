using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class CreateCategoryIntegrationTests : BaseIntegrationTest
{
    public CreateCategoryIntegrationTests(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ShouldReturnCreated()
    {
        // Arrange - Usando el builder CON DATOS DINÁMICOS
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData() // ← Ahora genera nombres únicos automáticamente
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Debug
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {content}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Opcional: Validar el contenido de la respuesta
        var categoryResponse = JsonSerializer.Deserialize<CategoryResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(categoryResponse);
        Assert.NotEmpty(categoryResponse.Name);
        Assert.NotEqual(Guid.Empty, categoryResponse.Id); // ✅ Para GUID
    }

    [Fact]
    public async Task CreateCategory_WithMinimalData_ShouldReturnCreated()
    {
        // Arrange
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithMinimalData() // ← También genera nombres únicos
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithEmptyName_ShouldReturnBadRequest()
    {
        // Arrange
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithEmptyName()
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithTooLongName_ShouldReturnBadRequest()
    {
        // Arrange
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithTooLongName()
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_ShouldReturnBadRequest()
    {
        // Arrange - Crear la primera categoría
        var categoryName = $"Duplicate Test Category {DateTime.UtcNow:yyyyMMddHHmmss}";

        var firstCategory = CreateCategoryTestDataBuilder
            .Create()
            .WithSpecificName(categoryName)
            .Build();

        // Crear la primera categoría
        var firstResponse = await Client.CreateCategoryAsync(firstCategory);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Intentar crear otra con el mismo nombre
        var duplicateCategory = CreateCategoryTestDataBuilder
            .Create()
            .WithSpecificName(categoryName) // ← Mismo nombre específico
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(duplicateCategory);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("La categoría ya existe", content);
    }
}