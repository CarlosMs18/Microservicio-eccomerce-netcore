using Cart.Application.Contracts.External;
using Cart.Application.DTos.External;
using Cart.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using Xunit;

namespace Cart.IntegrationTests.Common;

[Collection("Sequential")] // 🎯 Usar la collection para evitar paralelismo
public abstract class BaseIntegrationTest : IDisposable
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;

    // 🎯 Opciones de serialización reutilizables
    protected readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected BaseIntegrationTest(CustomWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        // ✅ Cliente por defecto CON autenticación
        Client = CreateClientWithTestUser();

        // 🧹 Limpiar BD en el constructor
        CleanDatabaseAsync().GetAwaiter().GetResult();
    }

    // 🧹 Método helper para limpiar BD manualmente si es necesario
    protected async Task CleanDatabaseAsync()
    {
        await Factory.CleanDatabaseAsync();
    }

    // 🧪 Helper para simular diferentes usuarios en tests
    protected HttpClient CreateClientWithTestUser(string userId = "test-user-123")
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-test-user-id", userId);
        return client;
    }

    // 🚫 Helper para simular usuario no autenticado
    protected HttpClient CreateUnauthenticatedClient()
    {
        // ✅ SOLUCIÓN: Crear un cliente completamente nuevo sin headers
        var client = Factory.CreateClient();
        // 🎯 Asegurarse de que NO tenga headers de autenticación
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("Authorization");
        return client;
    }

    // 🎯 Helper especial para testing sin autenticación con Factory específico
    protected HttpClient CreateClientWithoutAuth()
    {
        // ✅ Crear una factory específica que NO configure TestingAuth
        var factoryWithoutAuth = new CustomWebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 🚫 NO agregar TestingAuthentication para este cliente
                    // Solo servicios básicos
                });
            });

        return factoryWithoutAuth.CreateClient();
    }

    // 🎯 Helper para crear mock del CatalogService con datos por defecto
    protected Mock<ICatalogService> CreateMockCatalogService()
    {
        return new Mock<ICatalogService>();
    }

    // 🎯 Helper para configurar mock de producto válido
    protected Mock<ICatalogService> CreateMockCatalogServiceWithValidProduct(
        Guid productId,
        string productName = "Test Product",
        decimal price = 99.99m,
        int stock = 10)
    {
        var mock = new Mock<ICatalogService>();
        mock.Setup(x => x.ProductExistsAsync(productId)).ReturnsAsync(true);
        mock.Setup(x => x.GetProductStockAsync(productId)).ReturnsAsync(stock);
        mock.Setup(x => x.GetProductDetailsAsync(productId)).ReturnsAsync(new ProductDetailsDto
        {
            Id = productId,
            Name = productName,
            Description = "Test Description",
            Price = price,
            Category = new CategoryDto { Id = Guid.NewGuid(), Name = "Test Category" },
            Images = new List<ProductImageDto> { new ProductImageDto { ImageUrl = "test.jpg" } }
        });
        return mock;
    }

    // 🎯 Helper para crear cliente con mock del CatalogService (CON autenticación)
    protected HttpClient CreateClientWithMockedCatalogService(Mock<ICatalogService> mockCatalogService)
    {
        var client = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICatalogService));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(mockCatalogService.Object);
            });
        }).CreateClient();

        // ✅ Agregar autenticación por defecto
        client.DefaultRequestHeaders.Add("x-test-user-id", "test-user-123");
        return client;
    }

    // 🎯 Helper para crear cliente con mock del CatalogService (SIN autenticación)
    protected HttpClient CreateUnauthenticatedClientWithMockedCatalogService(Mock<ICatalogService> mockCatalogService)
    {
        var client = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICatalogService));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(mockCatalogService.Object);
            });
        }).CreateClient();

        // 🚫 NO agregar headers de autenticación
        return client;
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}