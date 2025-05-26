using Grpc.Core;
using Microsoft.Extensions.Logging;
using User.Auth;

namespace Catalog.Infrastructure.SyncDataServices.Grpc
{
    public interface IUserGrpcClient
    {
        Task<TokenValidationResponse> ValidateTokenAsync(string token);
    }

    public class UserGrpcClient : IUserGrpcClient
    {
        private readonly AuthService.AuthServiceClient _client;
        private readonly ILogger<UserGrpcClient> _logger;

        public UserGrpcClient(
            AuthService.AuthServiceClient client,
            ILogger<UserGrpcClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<TokenValidationResponse> ValidateTokenAsync(string token)
        {
            _logger.LogDebug("Validando token via gRPC...");

            try
            {
                var response = await _client.ValidateTokenAsync(
                    new TokenRequest { Token = token },
                    deadline: DateTime.UtcNow.AddSeconds(5));

                _logger.LogInformation("Token validado para usuario: {UserId}", response.UserId);
                return response;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                _logger.LogWarning("Timeout al validar token");
                throw new TimeoutException("Servicio de autenticación no respondió", ex);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al validar token");
                throw new Exception($"Error de autenticación: {ex.Status.Detail}");
            }
        }
    }
}