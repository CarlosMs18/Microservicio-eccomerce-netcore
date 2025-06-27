using Catalog.IntegrationTests.Builders;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Catalog.IntegrationTests.Common;

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
        Client = factory.CreateClient();

        // 🧹 Limpiar BD en el constructor
        CleanDatabaseAsync().GetAwaiter().GetResult();
    }

    // 🧹 Método helper para limpiar BD manualmente si es necesario
    protected async Task CleanDatabaseAsync()
    {
        await Factory.CleanDatabaseAsync();
    }

    // 🏗️ Helper para crear builder de categorías
    protected CategoryTestDataBuilder CategoryBuilder() => new();

    // 🌍 Helpers para configurar entorno
    protected void SetKubernetesEnvironment(bool isKubernetes = true)
    {
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST",
            isKubernetes ? "kubernetes.default.svc" : null);
    }

    protected void SetDockerEnvironment(bool isDocker = true)
    {
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER",
            isDocker ? "true" : null);
    }

    // 🧪 Helper para simular diferentes usuarios en tests
    protected HttpClient CreateClientWithTestUser(string userId = "test-user-123", string email = "test@example.com", string roles = "User")
    {
        return Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<Shared.Infrastructure.Authentication.TestingAuthOptions>(options =>
                {
                    options.DefaultUserId = userId;
                    options.DefaultUserEmail = email;
                    options.DefaultUserRoles = roles;
                });
            });
        }).CreateClient();
    }

    // 🚫 Helper para simular usuario no autenticado
    protected HttpClient CreateUnauthenticatedClient()
    {
        return Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<Shared.Infrastructure.Authentication.TestingAuthOptions>(options =>
                {
                    options.DefaultUserId = "";
                    options.DefaultUserEmail = "";
                    options.DefaultUserRoles = "";
                });
            });
        }).CreateClient();
    }

    public virtual void Dispose()
    {
        SetKubernetesEnvironment(false); // Reset environment
        SetDockerEnvironment(false); // Reset environment
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}