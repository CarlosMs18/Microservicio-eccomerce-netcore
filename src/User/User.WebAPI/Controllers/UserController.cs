using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Shared.Core.Dtos;
using Shared.Core.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using User.Application.DTOs.Requests;
using User.Application.Features.Users.Commands;

namespace User.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IExternalAuthService _externalAuthService;
     

        public UserController(IMediator mediator, IExternalAuthService externalAuthService)
        {
            _mediator = mediator;
            _externalAuthService = externalAuthService; 
        }

        [HttpPost("[action]")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult> RegisterUser([FromBody] RegistrationCommand command)
        {
            return Ok(await _mediator.Send(command));
        }

        [HttpPost("login")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _mediator.Send(new LoginCommand { Request = request });
            return Ok(response);
        }


        [HttpGet("validate-token")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TokenValidationDecoded))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> ValidateToken([FromHeader(Name = "Authorization")] string authHeader)
        {
            Console.WriteLine("LLAMANDO CONTROLADOR DE USER VALIDATE TOKEN HTTP!!");

            // ✅ SOLO en Kubernetes: Manejo completo de errores + headers
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            {
                try
                {
                    // Validación básica del token
                    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    {
                        return CreateErrorResponse(401, "Authentication required", "UNAUTHORIZED");
                    }

                    var token = authHeader["Bearer ".Length..].Trim();

                    // Validar el token usando el servicio externo
                    var result = await _externalAuthService.ValidateTokenAsync(token);

                    if (!result.IsValid)
                    {
                        return CreateErrorResponse(401, "Authentication required", "UNAUTHORIZED");
                    }

                    // Inyectar headers de respuesta para el Ingress
                    Response.Headers.Add("x-user-id", result.UserId ?? "");
                    Response.Headers.Add("x-user-email", result.Email ?? "");

                    // Convertir roles a string separado por comas
                    var rolesString = result.Roles != null && result.Roles.Any()
                        ? string.Join(",", result.Roles)
                        : "";
                    Response.Headers.Add("x-user-roles", rolesString);

                    Console.WriteLine($"🔐 Headers inyectados para Ingress - UserId: {result.UserId}, Email: {result.Email}, Roles: {rolesString}");

                    // En modo Kubernetes, devolver solo un 200 OK para el Ingress
                    return Ok(new { success = true, message = "Token valid" });
                }
                catch (HttpRequestException httpEx)
                {
                    // Error de conectividad con el servicio de autenticación
                    Console.WriteLine($"❌ Error de conectividad con servicio de auth: {httpEx.Message}");
                    return CreateErrorResponse(503, "Authentication service unavailable", "AUTH_SERVICE_UNAVAILABLE");
                }
                catch (TaskCanceledException timeoutEx)
                {
                    // Timeout del servicio de autenticación
                    Console.WriteLine($"⏰ Timeout en servicio de auth: {timeoutEx.Message}");
                    return CreateErrorResponse(503, "Authentication service unavailable", "AUTH_SERVICE_UNAVAILABLE");
                }
                catch (UnauthorizedAccessException)
                {
                    // Token inválido o expirado
                    return CreateErrorResponse(401, "Authentication required", "UNAUTHORIZED");
                }
                catch (Exception ex)
                {
                    // Error interno del servicio de autenticación
                    Console.WriteLine($"💥 Error interno en validación de token: {ex.Message}");
                    return CreateErrorResponse(500, "Authentication service error", "AUTH_SERVICE_ERROR");
                }
            }
            else
            {
                // 🔓 Otros entornos: Lógica simple sin manejo de errores específicos
                Console.WriteLine($"🔓 Entorno no kubernetes: Lógica simple");

                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized();

                var token = authHeader["Bearer ".Length..].Trim();
                var result = await _externalAuthService.ValidateTokenAsync(token);

                if (!result.IsValid)
                    return Unauthorized();

                return Ok(result);
            }
        }

        /// <summary>
        /// Crea una respuesta de error estandarizada compatible con el formato esperado por el Ingress
        /// </summary>
        private IActionResult CreateErrorResponse(int statusCode, string message, string error)
        {
            var errorResponse = new
            {
                success = false,
                message = message,
                error = error
            };

            // Asegurar que el Content-Type sea application/json
            Response.ContentType = "application/json";

            return StatusCode(statusCode, errorResponse);
        }
    }
}
