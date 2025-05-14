// SyncDataServices/Grpc/UserGrpcClient.cs
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using User.Auth; 

namespace Catalog.Infrastructure.SyncDataServices.Grpc
{
    public class UserGrpcClient : IDisposable // Implementa IDisposable para liberar recursos
    {
        private readonly GrpcChannel _channel;
        private readonly AuthService.AuthServiceClient _client; // Corregido: AuthService.AuthClient

        public UserGrpcClient(IConfiguration config)
        {
            // Configuración robusta del canal
            _channel = GrpcChannel.ForAddress(
                config["Grpc:UserUrl"] ?? throw new ArgumentNullException("Grpc:UserUrl no configurado"),
                new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
                    }
                });

            _client = new AuthService.AuthServiceClient(_channel); // Servicio: AuthService (no Auth)
        }

        public async Task<TokenValidationResponse> ValidateTokenAsync(string token)
        {
            Console.WriteLine("[UserGrpcClient] Validando token...");
            try
            {
                var response = await _client.ValidateTokenAsync(
                    new TokenRequest { Token = token },
                    deadline: DateTime.UtcNow.AddSeconds(5));

                Console.WriteLine($"[UserGrpcClient] Respuesta válida: {response.IsValid}");
                return response;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                Console.WriteLine("[UserGrpcClient] Timeout: Servidor gRPC no respondió");
                throw new TimeoutException("Timeout al validar el token", ex);
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"[UserGrpcClient] Error gRPC: {ex.Status.Detail}");
                throw new Exception($"Error gRPC: {ex.Status.Detail}");
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}