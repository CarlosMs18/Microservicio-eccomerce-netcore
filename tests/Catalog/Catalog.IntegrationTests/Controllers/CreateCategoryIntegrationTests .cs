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
    public async Task CreateCategory_WithBuilder_ShouldWork()
    {
        // Arrange - Usando el builder
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()
            .Build();

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Debug
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {content}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

}