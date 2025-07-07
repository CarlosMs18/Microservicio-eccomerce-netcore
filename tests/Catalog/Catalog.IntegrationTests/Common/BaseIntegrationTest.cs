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

        // 🎯 CAMBIO CLAVE: Cliente por defecto CON autenticación
        Client = CreateClientWithTestUser();

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
        var client = Factory.CreateClient();

        // ✅ AGREGAR HEADERS DE AUTENTICACIÓN
        client.DefaultRequestHeaders.Add("x-test-user-id", userId);
        client.DefaultRequestHeaders.Add("x-test-user-email", email);
        client.DefaultRequestHeaders.Add("x-test-user-roles", roles);

        return client;
    }

    // 🚫 Helper para simular usuario no autenticado
    protected HttpClient CreateUnauthenticatedClient()
    {
        var client = Factory.CreateClient();

        // 🎯 ASEGURARSE de que NO tenga headers de autenticación
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("x-test-user-email");
        client.DefaultRequestHeaders.Remove("x-test-user-roles");

        return client;
    }

    public virtual void Dispose()
    {
        SetKubernetesEnvironment(false); // Reset environment
        SetDockerEnvironment(false); // Reset environment
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}