using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class GetAllCategoriesIntegrationTests : BaseIntegrationTest
{
    public GetAllCategoriesIntegrationTests(CustomWebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnOkAndBeOrderedByName()
    {
        // Act
        await CategoryBuilder()
         .WithOrderedCategories()
         .SeedAsync(Factory.Services);

        // Act
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryListResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(categories);
        Assert.True(categories.Any()); // Al menos debe haber alguna

        // Verificar que están ordenadas por nombre (según tu handler)
        var sortedNames = categories.Select(c => c.Name).OrderBy(n => n).ToList();
        var actualNames = categories.Select(c => c.Name).ToList();

        Assert.Equal(sortedNames, actualNames);
    }

    [Fact]
    public async Task GetAllCategories_WhenCategoriesExist_ShouldReturnValidStructure()
    {
        // Arrange - Crear algunas categorías usando tu builder EXISTENTE
        var testPrefix = $"GETALL_TEST_{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var category1 = CreateCategoryTestDataBuilder
            .Create()
            .WithName($"{testPrefix}_Alpha")
            .WithDescription("Test category A")
            .Build();

        var category2 = CreateCategoryTestDataBuilder
            .Create()
            .WithName($"{testPrefix}_Beta")
            .WithDescription("Test category B")
            .Build();

        // Crear las categorías
        await Client.CreateCategoryAsync(category1);
        await Client.CreateCategoryAsync(category2);

        // Act
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryListResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(categories);

        // Verificar que nuestras categorías están ahí
        var testCategories = categories.Where(c => c.Name.StartsWith(testPrefix)).ToList();
        Assert.Equal(2, testCategories.Count);

        // Verificar estructura de CategoryListResponse
        Assert.All(testCategories, cat =>
        {
            Assert.NotEqual(Guid.Empty, cat.Id);
            Assert.NotEmpty(cat.Name);
            Assert.NotNull(cat.Description); // Asumiendo que siempre hay descripción
        });

        // Verificar que están ordenadas alfabéticamente
        var testCategoryNames = testCategories.Select(c => c.Name).ToList();
        Assert.True(testCategoryNames[0].EndsWith("Alpha"));
        Assert.True(testCategoryNames[1].EndsWith("Beta"));
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnCorrectResponseStructure()
    {
        // Act
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verificar Content-Type
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryListResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(categories);

        // Si hay categorías, verificar que tienen la estructura correcta
        if (categories.Any())
        {
            var firstCategory = categories.First();
            Assert.NotEqual(Guid.Empty, firstCategory.Id);
            Assert.NotEmpty(firstCategory.Name);
            // Description puede ser null, así que no la validamos como requerida
        }
    }

    [Fact]
    public async Task GetAllCategories_MultipleRequests_ShouldBeConsistent()
    {
        // Arrange - Crear una categoría para asegurar que hay datos
        var category = CreateCategoryTestDataBuilder
            .Create()
            .WithName($"CONSISTENCY_TEST_{DateTime.UtcNow:yyyyMMddHHmmssfff}")
            .Build();

        await Client.CreateCategoryAsync(category);

        // Act - Hacer múltiples requests
        var response1 = await Client.GetCategoriesAsync();
        var response2 = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        var categories1 = JsonSerializer.Deserialize<List<CategoryListResponse>>(content1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var categories2 = JsonSerializer.Deserialize<List<CategoryListResponse>>(content2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Las respuestas deben ser consistentes
        Assert.Equal(categories1.Count, categories2.Count);

        if (categories1.Any())
        {
            Assert.Equal(categories1.First().Id, categories2.First().Id);
        }
    }
}