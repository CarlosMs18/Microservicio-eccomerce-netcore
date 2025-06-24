using Catalog.Application.DTOs.Responses;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Authentication;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace Catalog.IntegrationTests.Controllers;

public class CategoryControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public CategoryControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnOkWithCategories()
    {
        // Act
        var response = await _client.GetAsync("/api/category");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content.ReadFromJsonAsync<IEnumerable<CategoryListResponse>>();
        Assert.NotNull(categories);

        // Verificar que la respuesta es una lista (puede estar vacía o con datos)
        Assert.IsAssignableFrom<IEnumerable<CategoryListResponse>>(categories);
    }

    [Fact]
    public async Task GetAllCategories_ShouldReturnJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/category");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetAllCategories_WithInvalidRoute_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/api/category/invalid-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithoutAuthentication_ShouldReturn401()
    {
        // Arrange - Crear un cliente sin autenticación
        var clientWithoutAuth = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Override para simular usuario no autenticado
                services.Configure<Shared.Infrastructure.Authentication.TestingAuthOptions>(options =>
                {
                    options.DefaultUserId = ""; // Usuario vacío simula no autenticado
                });
            });
        }).CreateClient();

        var createCommand = new
        {
            Name = "Test Category",
            Description = "Test Description"
        };

        // Act
        var response = await clientWithoutAuth.PostAsJsonAsync("/api/category/CreateCategory", createCommand);

        // Assert
        // Nota: Dependiendo de tu middleware, podría ser 401 o redirigir
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidData_ShouldReturn400()
    {
        // Arrange - Datos inválidos (nombre vacío)
        var invalidCommand = new
        {
            Name = "", // Nombre vacío debería fallar validación
            Description = "Test Description"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/category/CreateCategory", invalidCommand);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithMalformedJson_ShouldReturn400()
    {
        // Arrange - JSON malformado
        var malformedJson = "{ invalid json }";
        var content = new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/category/CreateCategory", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCategories_WithWrongHttpMethod_ShouldReturn405()
    {
        // Act - Usar POST en lugar de GET
        var response = await _client.PostAsync("/api/category", null);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}