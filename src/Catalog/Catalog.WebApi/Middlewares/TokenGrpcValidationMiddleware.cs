using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Catalog.Infrastructure.SyncDataServices.Grpc;

namespace Catalog.WebAPI.Middlewares
{
    public class TokenGrpcValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IUserGrpcClient _grpcClient;
        private readonly ILogger<TokenGrpcValidationMiddleware> _logger;
        private static readonly Dictionary<string, HashSet<string>> _publicRoutes = new()
        {
            ["/api/category"] = new HashSet<string> { "GET" },
            ["/health"] = new HashSet<string> { "GET" }
        };

        public TokenGrpcValidationMiddleware(
            RequestDelegate next,
            IUserGrpcClient grpcClient,
            ILogger<TokenGrpcValidationMiddleware> logger)
        {
            _next = next;
            _grpcClient = grpcClient;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant();
            var method = context.Request.Method;

            if (IsPublicRoute(method, path))
            {
                await _next(context);
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token requerido");
                return;
            }

            var token = authHeader["Bearer ".Length..].Trim();
            _logger.LogDebug("Validando token para ruta: {Path}", path);

            try
            {
                var response = await _grpcClient.ValidateTokenAsync(token);

                if (!response.IsValid)
                {
                    _logger.LogWarning("Token inválido para ruta: {Path}", path);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token inválido o expirado");
                    return;
                }

                var claims = new List<Claim>
                {
                    new Claim("uid", response.UserId),
                    new Claim(ClaimTypes.Email, response.Email)
                };
                claims.AddRange(response.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
                _logger.LogInformation("Acceso autorizado para usuario: {UserId}", response.UserId);
                await _next(context);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Servicio de autenticación no disponible");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Servicio de autenticación no disponible");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando token");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync($"Error validando token: {ex.Message}");
            }
        }

        private bool IsPublicRoute(string method, string path)
        {
            if (method == "OPTIONS") return true;
            if (string.IsNullOrEmpty(path)) return false;

            return _publicRoutes.Any(route =>
                path.StartsWith(route.Key) &&
                route.Value.Contains(method));
        }
    }
}