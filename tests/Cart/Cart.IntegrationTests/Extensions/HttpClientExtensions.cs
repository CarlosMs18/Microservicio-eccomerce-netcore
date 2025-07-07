using System.Net.Http.Json;

namespace Cart.IntegrationTests.Extensions;

public static class HttpClientExtensions
{
    // ✅ Método principal para agregar producto al carrito (CON autenticación)
    public static async Task<HttpResponseMessage> AddProductToCartAsync(
        this HttpClient client,
        object command)
    {
        // El cliente ya debe tener configurado el header x-test-user-id
        return await client.PostAsJsonAsync("/api/Cart/AddProductToCart", command);
    }

    // ✅ Para casos donde quieras un usuario específico
    public static async Task<HttpResponseMessage> AddProductToCartWithUserAsync(
        this HttpClient client,
        object command,
        string userId)
    {
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Add("x-test-user-id", userId);
        return await client.PostAsJsonAsync("/api/Cart/AddProductToCart", command);
    }

    // ✅ Para testing sin autenticación (debería fallar con 401)
    public static async Task<HttpResponseMessage> AddProductToCartWithoutAuthAsync(
        this HttpClient client,
        object command)
    {
        // 🎯 IMPORTANTE: Asegurarse de que NO tenga headers de autenticación
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Remove("Authorization");

        return await client.PostAsJsonAsync("/api/Cart/AddProductToCart", command);
    }

    // ✅ Otros endpoints del carrito
    public static async Task<HttpResponseMessage> GetCartAsync(
        this HttpClient client)
        => await client.GetAsync("/api/Cart");

    public static async Task<HttpResponseMessage> GetCartWithUserAsync(
        this HttpClient client,
        string userId)
    {
        client.DefaultRequestHeaders.Remove("x-test-user-id");
        client.DefaultRequestHeaders.Add("x-test-user-id", userId);
        return await client.GetAsync("/api/Cart");
    }

    public static async Task<HttpResponseMessage> RemoveProductFromCartAsync(
        this HttpClient client,
        object command)
        => await client.DeleteAsync($"/api/Cart/RemoveProduct"); // Ajusta según tu endpoint

    public static async Task<HttpResponseMessage> UpdateCartItemQuantityAsync(
        this HttpClient client,
        object command)
        => await client.PutAsJsonAsync("/api/Cart/UpdateQuantity", command); // Ajusta según tu endpoint

    public static async Task<HttpResponseMessage> ClearCartAsync(
        this HttpClient client)
        => await client.DeleteAsync("/api/Cart/Clear"); // Ajusta según tu endpoint
}