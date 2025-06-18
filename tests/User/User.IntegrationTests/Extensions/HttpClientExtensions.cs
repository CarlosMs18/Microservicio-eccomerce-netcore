using System.Net.Http.Headers;

namespace User.IntegrationTests.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> GetWithBearerTokenAsync(
            this HttpClient client,
            string requestUri,
            string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(request);
        }

        public static async Task<HttpResponseMessage> GetWithoutAuthorizationAsync(
            this HttpClient client,
            string requestUri)
        {
            return await client.GetAsync(requestUri);
        }

        // 🆕 Nuevo método para headers de authorization personalizados
        public static async Task<HttpResponseMessage> GetWithCustomAuthorizationAsync(
            this HttpClient client,
            string requestUri,
            string authorizationValue)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", authorizationValue);
            return await client.SendAsync(request);
        }

        // 🆕 Método para esquemas de authorization específicos
        public static async Task<HttpResponseMessage> GetWithAuthorizationSchemeAsync(
            this HttpClient client,
            string requestUri,
            string scheme,
            string parameter)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, parameter);
            return await client.SendAsync(request);
        }

        // 🆕 Método genérico para configurar requests
        public static async Task<HttpResponseMessage> GetWithRequestConfigurationAsync(
            this HttpClient client,
            string requestUri,
            Action<HttpRequestMessage> configureRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            configureRequest(request);
            return await client.SendAsync(request);
        }
    }
}