using Grpc.Core;
using Microsoft.Extensions.Logging;
using Shared.Core.Interfaces;
using User.Auth;

namespace User.Infrastructure.Services.External.Grpc
{
    public class AuthGrpcService : AuthService.AuthServiceBase
    {
        private readonly IExternalAuthService _authService;
        private readonly ILogger<AuthGrpcService> _logger;
        private readonly IHealthChecker _healthChecker;

        public AuthGrpcService(
            IExternalAuthService authService,
            ILogger<AuthGrpcService> logger,
            IHealthChecker healthChecker)
        {
            _authService = authService;
            _logger = logger;
            _healthChecker = healthChecker;
        }

        public override async Task<TokenValidationResponse> ValidateToken(
            TokenRequest request,
            ServerCallContext context)
        {
            Console.WriteLine("user grpc valdiate tokem");
            _logger.LogInformation("Validando token gRPC para usuario");

            var result = await _authService.ValidateTokenAsync(request.Token);

            var response = new TokenValidationResponse
            {
                IsValid = result.IsValid,
                UserId = result.UserId ?? string.Empty,
                Email = result.Email ?? string.Empty
            };

            // Mapeo de roles y claims
            response.Roles.AddRange(result.Roles ?? new List<string>());

            foreach (var claim in result.Claims)
            {
                response.Claims.Add(claim.Key, claim.Value);
            }

            return response;
        }
        public override async Task<HealthCheckResponse> CheckHealth(
        HealthCheckRequest request,
        ServerCallContext context)
        {
            var dbHealthy = await _healthChecker.CheckDatabaseHealthAsync();
            var depsHealthy = await _healthChecker.CheckExternalDependenciesAsync();
            var authHealthy = (await _authService.ValidateTokenAsync("test")) != null;

            var status = dbHealthy && depsHealthy && authHealthy
                ? HealthCheckResponse.Types.ServingStatus.Serving
                : HealthCheckResponse.Types.ServingStatus.NotServing;

            return new HealthCheckResponse { Status = status };
        }
    }

  
}
