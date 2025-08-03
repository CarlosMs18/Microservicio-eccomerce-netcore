using Azure.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Shared.Core.Dtos;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Interfaces;
using System.Diagnostics;
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
        private readonly IMetricsService _metricsService;

        public UserController(IMediator mediator, IExternalAuthService externalAuthService, IMetricsService metricsService)
        {
            _mediator = mediator;
            _externalAuthService = externalAuthService;
            _metricsService = metricsService;
        }

        [HttpPost("[action]")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnsupportedMediaType)]
        public async Task<ActionResult> RegisterUser([FromBody] RegistrationRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/user/registeruser";
            var method = "POST";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var response = await _mediator.Send(new RegistrationCommand { Request = request });
                return Ok(response);
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpPost("login")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/user/login";
            var method = "POST";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var response = await _mediator.Send(new LoginCommand { Request = request });
                return Ok(response);
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("validate-token")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TokenValidationDecoded))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> ValidateToken()
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/user/validate-token";
            var method = "GET";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            Console.WriteLine("LLAMANDO CONTROLADOR DE USER VALIDATE TOKEN HTTP!!");

            // ✅ DETECCIÓN SIMPLE: Solo Production y Kubernetes usan Ingress
            var isIngressEnvironment = IsIngressEnvironment();

            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                // ✅ Validación básica del token (común para ambos entornos)
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    if (isIngressEnvironment)
                        return CreateErrorResponse(401, "Authentication required", "UNAUTHORIZED");
                    else
                        return Unauthorized();
                }

                var token = authHeader["Bearer ".Length..].Trim();

                // ✅ Validar el token usando el servicio externo
                // El servicio ya maneja todas las excepciones y retorna IsValid
                var result = await _externalAuthService.ValidateTokenAsync(token);

                if (!result.IsValid)
                {
                    if (isIngressEnvironment)
                        return CreateErrorResponse(401, "Authentication required", "UNAUTHORIZED");
                    else
                        return Unauthorized();
                }

                // ✅ Respuesta exitosa según el entorno
                if (isIngressEnvironment)
                {
                    // 🔐 PRODUCTION/KUBERNETES: Inyectar headers de respuesta para el Ingress
                    Response.Headers.Add("x-user-id", result.UserId ?? "");
                    Response.Headers.Add("x-user-email", result.Email ?? "");

                    // Convertir roles a string separado por comas
                    var rolesString = result.Roles != null && result.Roles.Any()
                        ? string.Join(",", result.Roles)
                        : "";
                    Response.Headers.Add("x-user-roles", rolesString);

                    Console.WriteLine($"🔐 Headers inyectados para Ingress - UserId: {result.UserId}, Email: {result.Email}, Roles: {rolesString}");

                    return Ok(new { success = true, message = "Token valid" });
                }
                else
                {
                    // 🔓 OTROS ENTORNOS: Devolver el objeto completo
                    Console.WriteLine($"🔓 Entorno sin Ingress: Devolviendo objeto completo");
                    return Ok(result);
                }
            }
            catch (HttpRequestException httpEx)
            {
                // ❌ Error de conectividad con servicios externos (si los hubiera)
                Console.WriteLine($"❌ Error de conectividad: {httpEx.Message}");

                if (isIngressEnvironment)
                    return CreateErrorResponse(503, "Authentication service unavailable", "AUTH_SERVICE_UNAVAILABLE");
                else
                    return StatusCode(503, new { error = "Authentication service unavailable", message = httpEx.Message });
            }
            catch (TaskCanceledException timeoutEx)
            {
                // ⏰ Timeout
                Console.WriteLine($"⏰ Timeout: {timeoutEx.Message}");

                if (isIngressEnvironment)
                    return CreateErrorResponse(503, "Authentication service unavailable", "AUTH_SERVICE_UNAVAILABLE");
                else
                    return StatusCode(503, new { error = "Authentication service timeout", message = timeoutEx.Message });
            }
            catch (Exception ex)
            {
                // 💥 Error interno no esperado
                Console.WriteLine($"💥 Error interno en validación de token: {ex.Message}");
                Console.WriteLine($"💥 Stack trace: {ex.StackTrace}");

                if (isIngressEnvironment)
                    return CreateErrorResponse(500, "Authentication service error", "AUTH_SERVICE_ERROR");
                else
                    return StatusCode(500, new { error = "Internal authentication error", message = ex.Message });
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("test")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult Test()
        {
            Console.WriteLine("OK");

            return Ok("OK");
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
                error = error,
                timestamp = DateTime.UtcNow
            };

            // Asegurar que el Content-Type sea application/json
            Response.ContentType = "application/json";

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Detecta si estamos en un entorno que usa Ingress (Production o Kubernetes)
        /// ✅ SIMPLE Y DIRECTO: Solo dos casos específicos
        /// </summary>
        private bool IsIngressEnvironment()
        {
            // Production: ASPNETCORE_ENVIRONMENT=Production
            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (aspnetEnv == "Production")
                return true;

            // Kubernetes: detectar por variable específica
            var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
            return isKubernetes;
        }
    }
}