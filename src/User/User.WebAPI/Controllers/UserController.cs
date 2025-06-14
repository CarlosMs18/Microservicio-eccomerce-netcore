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
        public async Task<IActionResult> ValidateToken([FromHeader(Name = "Authorization")] string authHeader)
        {
            Console.WriteLine("LLAMANDO CONTROLADOR DE USER VALIDATE TOKEN HTTP!!");

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized();

            var token = authHeader["Bearer ".Length..].Trim();
            var result = await _externalAuthService.ValidateTokenAsync(token);

            if (!result.IsValid)
                return Unauthorized();

            // ✅ SOLO en Kubernetes: Inyectar headers para Ingress
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            {
                Response.Headers.Add("x-user-id", result.UserId ?? "");
                Response.Headers.Add("x-user-email", result.Email ?? "");

                // Convertir roles a string separado por comas
                var rolesString = result.Roles != null && result.Roles.Any()
                    ? string.Join(",", result.Roles)
                    : "";
                Response.Headers.Add("x-user-roles", rolesString);

                Console.WriteLine($"🔐 Headers inyectados para Ingress - UserId: {result.UserId}, Email: {result.Email}, Roles: {rolesString}");
            }
            else
            {
                Console.WriteLine($"🔓 Entorno no kubernetes: Solo devolviendo JSON (sin headers)");
            }

            return Ok(result);
        }
    }
}
