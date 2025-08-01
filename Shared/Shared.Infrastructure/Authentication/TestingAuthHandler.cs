﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Shared.Infrastructure.Authentication;

public class TestingAuthHandler : AuthenticationHandler<TestingAuthOptions>
{
    public TestingAuthHandler(
        IOptionsMonitor<TestingAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogWarning("🧪 TestingAuthHandler ejecutándose...");

        try
        {
            // 🎯 CAMBIO CLAVE: Verificar si existe el header requerido
            var userId = Request.Headers["x-test-user-id"].FirstOrDefault();

            // 🚨 Si NO hay header de usuario, FALLAR la autenticación
            if (string.IsNullOrEmpty(userId))
            {
                Logger.LogWarning("❌ No se encontró header 'x-test-user-id' - Autenticación fallida");
                return Task.FromResult(AuthenticateResult.Fail("Header de usuario requerido no encontrado"));
            }

            // ✅ Si hay header, proceder con autenticación exitosa
            var userEmail = Request.Headers["x-test-user-email"].FirstOrDefault()
                           ?? Options.DefaultUserEmail;
            var userRoles = Request.Headers["x-test-user-roles"].FirstOrDefault()
                           ?? Options.DefaultUserRoles;

            // Crear claims con datos de prueba
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("user_id", userId)
            };

            // Agregar email
            if (!string.IsNullOrEmpty(userEmail))
            {
                claims.Add(new Claim(ClaimTypes.Email, userEmail));
            }

            // Agregar roles
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

            Logger.LogDebug("✅ Usuario de testing autenticado: {UserId}", userId);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "💥 Error en autenticación de testing");
            return Task.FromResult(AuthenticateResult.Fail("Error interno de autenticación"));
        }
    }
}