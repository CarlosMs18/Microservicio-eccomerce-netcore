using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class CategoryGetControllerTests : BaseIntegrationTest
{
    public CategoryGetControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnOkWithCategories()
    {
        // Act
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content.ReadFromJsonAsync<IEnumerable<CategoryListResponse>>(JsonOptions);
        Assert.NotNull(categories);

        // Verificar que la respuesta es una lista (puede estar vacía o con datos)
        Assert.IsAssignableFrom<IEnumerable<CategoryListResponse>>(categories);
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnJsonContentType()
    {
        // Act
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetAllCategories_WithInvalidRoute_ShouldReturn404()
    {
        // Act
        var response = await Client.GetAsync("/api/category/invalid-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCategories_WithWrongHttpMethod_ShouldReturn405()
    {
        // Act - Usar POST en lugar de GET
        var response = await Client.PostAsync("/api/category", null);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}