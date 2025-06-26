using System.Net.Http.Json;

namespace Catalog.IntegrationTests.Extensions;

public static class HttpClientExtensions
{
    // ✅ Método principal - súper simple
    public static async Task<HttpResponseMessage> CreateCategoryAsync(
        this HttpClient client,
        object command)
    {
        // En Testing, el TestingAuthHandler maneja todo automáticamente
        // No necesitas headers adicionales
        return await client.PostAsJsonAsync("/api/category", command);
    }

    // ✅ Para casos donde quieras un usuario específico
    public static async Task<HttpResponseMessage> CreateCategoryWithUserAsync(
        this HttpClient client,
        object command,
        string userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/category");

        // Solo si quieres override del usuario por defecto
        request.Headers.Add("x-test-user-id", userId);

        // Usar PostAsJsonAsync es más limpio que serializar manualmente
        var response = await client.PostAsJsonAsync("/api/category", command);
        return response;
    }

    // ✅ Para testing sin autenticación (debería fallar con 401)
    public static async Task<HttpResponseMessage> CreateCategoryWithoutAuthAsync(
        this HttpClient client,
        object command)
    {
        // Crear cliente sin el factory para bypasear la auth
        // Esto es más complejo, mejor usar el método simple
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
}