using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Core.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using User.Application.Models;
using Shared.Core.Dtos;
using System.Security.Claims;
using User.Application.Constants;

public class ExternalAuthService : IExternalAuthService
{
    private readonly JwtSettings _jwtSettings; // Inyecta configuración JWT

    public ExternalAuthService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<TokenValidationDecoded> ValidateTokenAsync(string token)
    {
        Console.WriteLine("LLAMANDO METODO VALIDATE TOKEN ASYNC DE USER");
        var result = new TokenValidationDecoded();

        try
        {
            // Validación de token nulo o vacío ANTES de procesar
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Token is null or empty");
                result.IsValid = false;
                return result;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Validación principal del token
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            result.IsValid = true;

            // Extraemos las claims importantes
            result.UserId = principal.FindFirst(CustomClaimTypes.UID)?.Value;
            result.Email = principal.FindFirst(ClaimTypes.Email)?.Value;

            // Extraemos todos los roles
            result.Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            // Opcional: Extraer todas las claims como diccionario
            foreach (var claim in principal.Claims)
            {
                result.Claims[claim.Type] = claim.Value;
            }

            return result;
        }
        catch (SecurityTokenMalformedException ex)
        {
            // Token malformado (formato incorrecto)
            Console.WriteLine($"Token malformado: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (SecurityTokenExpiredException ex)
        {
            // Token expirado
            Console.WriteLine($"Token expirado: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            // Issuer inválido
            Console.WriteLine($"Issuer inválido: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            // Audience inválido
            Console.WriteLine($"Audience inválido: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            // Firma inválida
            Console.WriteLine($"Firma inválida: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (SecurityTokenException ex)
        {
            // Cualquier otra excepción de SecurityToken
            Console.WriteLine($"Error de seguridad del token: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (ArgumentException ex)
        {
            // Argumentos inválidos
            Console.WriteLine($"Argumentos inválidos: {ex.Message}");
            result.IsValid = false;
            return result;
        }
        catch (Exception ex)
        {
            // Cualquier otra excepción no esperada
            Console.WriteLine($"Error inesperado en validación de token: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }
}