using Microsoft.AspNetCore.Http;
using Shared.Core.Dtos;
using Shared.Core.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    // Diccionario de rutas públicas con sus métodos permitidos
    private static readonly Dictionary<string, HashSet<string>> _publicRoutes = new()
    {
        ["/api/category"] = new HashSet<string> { "GET" },
        // Ejemplo de cómo agregar más rutas:
        // ["/api/product"] = new HashSet<string> { "GET", "OPTIONS" },
        // ["/api/public/data"] = new HashSet<string> { "GET" }
    };

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IExternalAuthService authService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var method = context.Request.Method;

        // 1. Verificar si es una ruta pública
        if (IsPublicRoute(method, path))
        {
            await _next(context);
            return;
        }

        // 2. Validar token para rutas protegidas
        var authHeader = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token requerido");
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        Console.WriteLine($"TOKEN: {token}");
        var tokenResult = await authService.ValidateTokenAsync(token);

        if (tokenResult == null || !tokenResult.IsValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token inválido o expirado");
            return;
        }

        // 3. Construir identidad del usuario
        var identity = CreateIdentity(tokenResult);
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }

    private bool IsPublicRoute(string method, string path)
    {
        // Todas las peticiones OPTIONS son públicas (necesario para CORS)
        if (method == "OPTIONS")
            return true;

        if (string.IsNullOrEmpty(path))
            return false;

        // Buscar coincidencia exacta o prefijo de ruta
        foreach (var route in _publicRoutes)
        {
            if (path.StartsWith(route.Key))
            {
                // Verificar si el método está permitido
                return route.Value.Contains(method);
            }
        }

        return false;
    }

    private static ClaimsIdentity CreateIdentity(TokenValidationDecoded tokenResult)
    {
        var claims = new List<Claim>
        {
            new Claim("uid", tokenResult.UserId),
            new Claim(ClaimTypes.Email, tokenResult.Email)
        };

        foreach (var role in tokenResult.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsIdentity(claims, "Bearer");
    }
}