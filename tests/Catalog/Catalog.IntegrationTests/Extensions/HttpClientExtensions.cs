using System.Net.Http.Json;

namespace Catalog.IntegrationTests.Extensions;

public static class HttpClientExtensions
{
    // ✅ Método principal para crear categoría (CON autenticación)
    public static async Task<HttpResponseMessage> CreateCategoryAsync(
        this HttpClient client,
        object command)
    {
        // El cliente ya debe tener configurado el header x-test-user-id
        return await client.PostAsJsonAsync("/api/category", command);
    }

    // ✅ Para casos donde quieras un usuario específico
    public static async Task<HttpResponseMessage> CreateCategoryWithUserAsync(
        this HttpClient client,
        object command,
        string userId)
    {
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Add("x-test-user-id", userId);
        return await client.PostAsJsonAsync("/api/category", command);
    }

    // ✅ Para testing sin autenticación (debería fallar con 401)
    public static async Task<HttpResponseMessage> CreateCategoryWithoutAuthAsync(
        this HttpClient client,
        object command)
    {
        // 🎯 IMPORTANTE: Asegurarse de que NO tenga headers de autenticación
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("x-test-user-email");
        client.DefaultRequestHeaders.Remove("x-test-user-roles");
        return await client.PostAsJsonAsync("/api/category", command);
    }

    // ✅ Otros endpoints básicos
    public static async Task<HttpResponseMessage> GetCategoriesAsync(
        this HttpClient client)
        => await client.GetAsync("/api/category");

    public static async Task<HttpResponseMessage> GetCategoryAsync(
        this HttpClient client,
        Guid id)
        => await client.GetAsync($"/api/category/{id}");

    // ✅ Versiones específicas para testing sin autenticación
    public static async Task<HttpResponseMessage> GetCategoriesWithoutAuthAsync(
        this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("x-test-user-email");
        client.DefaultRequestHeaders.Remove("x-test-user-roles");
        return await client.GetAsync("/api/category");
    }

    public static async Task<HttpResponseMessage> GetCategoryWithoutAuthAsync(
        this HttpClient client,
        Guid id)
    {
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("x-test-user-email");
        client.DefaultRequestHeaders.Remove("x-test-user-roles");
        return await client.GetAsync($"/api/category/{id}");
    }
}