using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shared.Core.Dtos;
using Shared.Core.Interfaces;

namespace Catalog.Infrastructure.SyncDataServices
{
    public class UserHttpService : IExternalAuthService
    {
        private readonly HttpClient _httpClient;

        // Constructor simple - Toda la configuración se hace en Program.cs
        public UserHttpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<TokenValidationDecoded> ValidateTokenAsync(string token)
        {
            try
            {
                Console.WriteLine("llamando ValidateTokenAsync DE CATALOG ");
                var request = new HttpRequestMessage(HttpMethod.Get, "validate-token");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return InvalidTokenResult();

                var result = await response.Content.ReadFromJsonAsync<TokenValidationDecoded>();
                return result ?? InvalidTokenResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserHttpService] Error: {ex.Message}");
                return InvalidTokenResult();
            }
        }

        private static TokenValidationDecoded InvalidTokenResult() =>
            new() { IsValid = false };
    }
}