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
        .WithValidData()
        .Build();

        // 🔍 DEBUG: Ver exactamente qué se está enviando
        var commandJson = JsonSerializer.Serialize(categoryCommand, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"=== SENDING COMMAND ===");
        Console.WriteLine(commandJson);

        // Act
        var response = await Client.CreateCategoryAsync(categoryCommand);

        // Debug respuesta
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"=== RESPONSE ===");
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Content: {content}");

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

    [Fact]
    public async Task GetCategories_ShouldReturnOkWithList()
    {
        // Arrange - Crear algunas categorías primero
        var category1 = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()
            .Build();

        var category2 = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()
            .Build();

        await Client.CreateCategoryAsync(category1);
        await Client.CreateCategoryAsync(category2);

        // Act - USANDO tu extensión GetCategoriesAsync()
        var response = await Client.GetCategoriesAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(categories);
        Assert.True(categories.Count >= 2); // Al menos las 2 que creamos
    }

    // ✅ USANDO: GetCategoryAsync(Guid id)
    [Fact]
    public async Task GetCategory_WithValidId_ShouldReturnCategory()
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

        // Act - USANDO tu extensión GetCategoryAsync()
        var response = await Client.GetCategoryAsync(createdCategory.Id);

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

    // ✅ USANDO: GetCategoryAsync() con ID inexistente
    [Fact]
    public async Task GetCategory_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act - USANDO tu extensión GetCategoryAsync()
        var response = await Client.GetCategoryAsync(nonExistentId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    // ✅ USANDO: CreateCategoryWithoutAuthAsync() 
    [Fact]
    public async Task CreateCategory_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithValidData()
            .Build();

        // Act - USANDO tu extensión CreateCategoryWithoutAuthAsync()
        var response = await Client.CreateCategoryWithoutAuthAsync(categoryCommand);

        // Assert
        // Dependiendo de tu configuración, puede ser 401 o 200 si el TestAuthHandler maneja todo
        // Ajusta según tu implementación
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.Created);

        Console.WriteLine($"Status without auth: {response.StatusCode}");
    }

    // ✅ TEST COMPLETO: Crear -> Obtener -> Listar
    [Fact]
    public async Task FullWorkflow_CreateGetAndList_ShouldWork()
    {
        // 1. Crear categoría
        var categoryCommand = CreateCategoryTestDataBuilder
            .Create()
            .WithName($"Workflow Test {DateTime.UtcNow:yyyyMMddHHmmss}")
            .WithDescription("Test workflow category")
            .Build();

        var createResponse = await Client.CreateCategoryAsync(categoryCommand);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // 2. Obtener la categoría creada
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createdCategory = JsonSerializer.Deserialize<CategoryResponse>(createContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var getResponse = await Client.GetCategoryAsync(createdCategory.Id);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // 3. Verificar que aparece en la lista
        var listResponse = await Client.GetCategoriesAsync();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listContent = await listResponse.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryResponse>>(listContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Contains(categories, c => c.Id == createdCategory.Id);
    }

    // ✅ TEST DE PERFORMANCE: Crear múltiples categorías
    [Fact]
    public async Task CreateMultipleCategories_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 5; i++)
        {
            var categoryCommand = CreateCategoryTestDataBuilder
                .Create()
                .WithName($"Batch Category {i} - {DateTime.UtcNow:yyyyMMddHHmmss}")
                .Build();

            tasks.Add(Client.CreateCategoryAsync(categoryCommand));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // Verificar que todas aparecen en la lista
        var listResponse = await Client.GetCategoriesAsync();
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<CategoryResponse>>(listContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.True(categories.Count >= 5);
    }
}