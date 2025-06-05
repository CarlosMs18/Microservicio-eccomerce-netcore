using Catalog.Infrastructure.SyncDataServices.Grpc;
using System.Security.Claims;

namespace Catalog.WebAPI.Middlewares
{
    public class TokenGrpcValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IUserGrpcClient _grpcClient;
        private readonly ILogger<TokenGrpcValidationMiddleware> _logger;

        private static readonly Dictionary<string, HashSet<string>> _publicRoutes = new()
        {
            ["/api/product/getallproducts"] = new HashSet<string> { "GET" },
            ["/api/product/getproductbyid"] = new HashSet<string> { "GET" },
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

            // ✅ SOLUCIÓN: Verificar si es una ruta pública (incluye gRPC)
            if (IsPublicRoute(context, method, path))
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

        private bool IsPublicRoute(HttpContext context, string method, string path)
        {
            // ✅ Permitir requests OPTIONS (CORS)
            if (method == "OPTIONS") return true;

            // ✅ Validar path no nulo
            if (string.IsNullOrEmpty(path)) return false;

            // ✅ SOLUCIÓN PRINCIPAL: Excluir todas las comunicaciones gRPC internas
            // Opción 1: Por Content-Type
            if (context.Request.ContentType?.Contains("application/grpc") == true)
            {
                _logger.LogDebug("🔓 Permitiendo comunicación gRPC interna: {Path}", path);
                return true;
            }

            // Opción 2: Por protocolo HTTP/2 + path que no sea REST API
            if (context.Request.Protocol == "HTTP/2" && !path.StartsWith("/api/"))
            {
                _logger.LogDebug("🔓 Permitiendo comunicación gRPC (HTTP/2): {Path}", path);
                return true;
            }

            // Opción 3: Por patrones de path gRPC conocidos
            if (IsGrpcPath(path))
            {
                _logger.LogDebug("🔓 Permitiendo comunicación gRPC por path: {Path}", path);
                return true;
            }

            // ✅ Verificar rutas públicas REST configuradas
            bool isPublicRestRoute = _publicRoutes.Any(route =>
                path.StartsWith(route.Key) &&
                route.Value.Contains(method));

            if (isPublicRestRoute)
            {
                _logger.LogDebug("🔓 Permitiendo ruta REST pública: {Method} {Path}", method, path);
                return true;
            }

            // ✅ Si no es pública, requiere autenticación
            _logger.LogDebug("🔒 Ruta requiere autenticación: {Method} {Path}", method, path);
            return false;
        }

        private bool IsGrpcPath(string path)
        {
            // Patrones comunes de rutas gRPC
            var grpcPatterns = new[]
            {
                "/catalog.",           // /catalog.CatalogProtoService/
                "/grpc/",             // Si usas prefijo custom
                "grpc",               // Cualquier path que contenga 'grpc'
                "/proto/",            // Si usas prefijo proto
            };

            return grpcPatterns.Any(pattern =>
                path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }
}