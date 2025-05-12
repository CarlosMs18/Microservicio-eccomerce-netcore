using Microsoft.AspNetCore.Http;
using Shared.Core.Dtos;
using Shared.Core.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IExternalAuthService authService)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var method = context.Request.Method;

        // 1. Permitir sin token: GET a /api/category
        if (method == "GET" && path.StartsWith("/api/category"))
        {
            await _next(context);
            return;
        }

        // 2. Permitir sin token: OPTIONS (necesario para CORS)
        if (method == "OPTIONS")
        {
            await _next(context);
            return;
        }

        // 3. Validar token para rutas protegidas
        var authHeader = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token requerido");
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var tokenResult = await authService.ValidateTokenAsync(token);
        Console.WriteLine("TOKEN RESULT", tokenResult);

        if (tokenResult == null || !tokenResult.IsValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token inválido o expirado");
            return;
        }

        // 4. Construir identidad del usuario autenticado
        var claims = new List<Claim>
        {
            new Claim("uid", tokenResult.UserId),
            new Claim(ClaimTypes.Email, tokenResult.Email)
        };

        // Agregar roles si existen
        foreach (var role in tokenResult.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // 5. Continuar con el pipeline
        await _next(context);
    }
}