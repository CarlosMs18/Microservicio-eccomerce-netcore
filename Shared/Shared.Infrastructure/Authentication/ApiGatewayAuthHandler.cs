using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Shared.Infrastructure.Authentication;

public class ApiGatewayAuthHandler : AuthenticationHandler<ApiGatewayAuthOptions>
{
    public ApiGatewayAuthHandler(
        IOptionsMonitor<ApiGatewayAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) // ✅ Usa Microsoft.AspNetCore.Authentication.ISystemClock
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Ahora puedes usar las opciones configurables si quieres
            var userId = Request.Headers["x-user-id"].FirstOrDefault();
            var userEmail = Request.Headers["x-user-email"].FirstOrDefault();
            var userRoles = Request.Headers["x-user-roles"].FirstOrDefault();

            // Si no hay user-id, significa que el Gateway no pudo validar el token
            if (string.IsNullOrEmpty(userId))
            {
                Logger.LogWarning("No se encontró x-user-id en los headers del API Gateway");
                return Task.FromResult(AuthenticateResult.Fail("Usuario no autenticado por el API Gateway"));
            }

            // Crear claims basados en la información del Gateway
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("user_id", userId)
            };

            // Agregar email si está disponible
            if (!string.IsNullOrEmpty(userEmail))
            {
                claims.Add(new Claim(ClaimTypes.Email, userEmail));
            }

            // Agregar roles si están disponibles
            if (!string.IsNullOrEmpty(userRoles))
            {
                var roles = userRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
                }
            }

            // Crear identidad y principal
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogDebug("Usuario autenticado exitosamente desde API Gateway: {UserId}", userId);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error al procesar autenticación desde API Gateway");
            return Task.FromResult(AuthenticateResult.Fail("Error interno de autenticación"));
        }
    }
}