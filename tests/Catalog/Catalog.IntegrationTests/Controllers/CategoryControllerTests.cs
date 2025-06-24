using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

public class CategoryControllerTests : BaseIntegrationTest
{
    public CategoryControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory)
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
    public async Task CreateCategory_WithoutAuthentication_ShouldReturn401()
    {
        // Arrange
        var unauthenticatedClient = CreateUnauthenticatedClient();
        var categoryData = CategoryTestDataBuilder.Create().WithValidData().Build();

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/category/CreateCategory", categoryData);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var categoryData = CategoryTestDataBuilder.Create().WithValidData().Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryData, options: JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidData_ShouldReturn400()
    {
        // Arrange
        var invalidCategoryData = CategoryTestDataBuilder.Create().WithInvalidData().Build();

        // Act
        var response = await Client.CreateCategoryAsync(invalidCategoryData, options: JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithCustomUser_ShouldReturnCreated()
    {
        // Arrange
        var customUserId = "custom-user-456";
        var categoryData = CategoryTestDataBuilder.Create()
            .WithName("Custom User Category")
            .WithDescription("Category created by custom user")
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryData, customUserId, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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