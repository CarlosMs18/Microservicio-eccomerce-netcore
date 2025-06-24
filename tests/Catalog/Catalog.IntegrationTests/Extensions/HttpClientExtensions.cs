using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Catalog.IntegrationTests.Extensions;

public static class HttpClientExtensions
{
    // 🔍 Métodos GET con diferentes configuraciones de autenticación
    public static async Task<HttpResponseMessage> GetWithTestUserAsync(
        this HttpClient client,
        string requestUri,
        string userId = "test-user-123")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("x-test-user-id", userId);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> GetWithoutAuthorizationAsync(
        this HttpClient client,
        string requestUri)
    {
        return await client.GetAsync(requestUri);
    }

    // 📝 Métodos POST con JSON
    public static async Task<HttpResponseMessage> PostAsJsonWithTestUserAsync<T>(
        this HttpClient client,
        string requestUri,
        T data,
        string userId = "test-user-123",
        JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(data, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        request.Headers.Add("x-test-user-id", userId);

        return await client.SendAsync(request);
    }

    // 🔧 Métodos para configurar headers de testing personalizados
    public static async Task<HttpResponseMessage> GetWithCustomTestHeadersAsync(
        this HttpClient client,
        string requestUri,
        string userId,
        string userEmail,
        string userRoles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("x-test-user-id", userId);
        request.Headers.Add("x-test-user-email", userEmail);
        request.Headers.Add("x-test-user-roles", userRoles);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PostWithCustomTestHeadersAsync<T>(
        this HttpClient client,
        string requestUri,
        T data,
        string userId,
        string userEmail,
        string userRoles,
        JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(data, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        request.Headers.Add("x-test-user-id", userId);
        request.Headers.Add("x-test-user-email", userEmail);
        request.Headers.Add("x-test-user-roles", userRoles);

        return await client.SendAsync(request);
    }

    // 🆕 Método genérico para configurar requests
    public static async Task<HttpResponseMessage> SendWithRequestConfigurationAsync(
        this HttpClient client,
        HttpMethod method,
        string requestUri,
        Action<HttpRequestMessage> configureRequest,
        object? data = null,
        JsonSerializerOptions? options = null)
    {
        var request = new HttpRequestMessage(method, requestUri);

        if (data != null)
        {
            var json = JsonSerializer.Serialize(data, options);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        configureRequest(request);
        return await client.SendAsync(request);
    }

    // 🎯 Helpers específicos para el contexto de Catalog
    public static async Task<HttpResponseMessage> GetCategoriesAsync(
        this HttpClient client,
        string userId = "test-user-123")
    {
        return await client.GetWithTestUserAsync("/api/category", userId);
    }

    public static async Task<HttpResponseMessage> CreateCategoryAsync<T>(
        this HttpClient client,
        T categoryData,
        string userId = "test-user-123",
        JsonSerializerOptions? options = null)
    {
        return await client.PostAsJsonWithTestUserAsync("/api/category/CreateCategory", categoryData, userId, options);
    }

    public static async Task<HttpResponseMessage> GetProductsAsync(
        this HttpClient client,
        string userId = "test-user-123")
    {
        return await client.GetWithTestUserAsync("/api/product", userId);
    }
}