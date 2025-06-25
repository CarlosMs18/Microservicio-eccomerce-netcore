using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Extensions;
using Catalog.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class CategoryCreateControllerTests : BaseIntegrationTest
{
    public CategoryCreateControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateCategory_SuperSimple_ShouldWork()
    {
        // Arrange
        var categoryData = CategoryTestDataBuilder.Create().WithValidData().Build();

        // Act - Usar tu extensión que ya maneja headers
        var response = await Client.PostAsJsonWithTestUserAsync(
            "/api/category/CreateCategory",
            categoryData,
            "test-user-123",
            JsonOptions);

        // Assert
        Console.WriteLine($"Status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
        var authenticatedClient = CreateClientWithTestUser(); // ✅ Cliente autenticado explícito
        var categoryData = CategoryTestDataBuilder.Create().WithValidData().Build();

        // Act
        var response = await authenticatedClient.CreateCategoryAsync(categoryData, options: JsonOptions);

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
}