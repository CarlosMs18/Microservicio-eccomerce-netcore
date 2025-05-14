using Grpc.Core;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Catalog.Infrastructure.SyncDataServices.Grpc; // 👈 Añade este namespace

public class TokenGrpcValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly UserGrpcClient _grpcClient; // 👈 Usa tu clase personalizada
    private static readonly Dictionary<string, HashSet<string>> _publicRoutes = new()
    {
        ["/api/category"] = new HashSet<string> { "GET" }
    };

    // Inyecta UserGrpcClient en lugar de AuthService.AuthServiceClient
    public TokenGrpcValidationMiddleware(RequestDelegate next, UserGrpcClient grpcClient)
    {
        _next = next;
        _grpcClient = grpcClient; // 👈 Asigna el cliente
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
        Console.WriteLine($"[gRPC Middleware] Token recibido: {token}");

        try
        {
            // 👇 Usa tu UserGrpcClient en lugar del cliente autogenerado
            var response = await _grpcClient.ValidateTokenAsync(token);

            if (!response.IsValid)
            {
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
            await _next(context);
        }
        catch (TimeoutException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Servicio de autenticación no disponible");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync($"Error validando token: {ex.Message}");
        }
    }

    private bool IsPublicRoute(string method, string path)
    {
        if (method == "OPTIONS") return true;
        if (string.IsNullOrEmpty(path)) return false;

        foreach (var route in _publicRoutes)
        {
            if (path.StartsWith(route.Key) && route.Value.Contains(method))
                return true;
        }
        return false;
    }
}


