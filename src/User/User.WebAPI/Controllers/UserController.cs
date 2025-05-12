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

            Console.WriteLine("LLAMANDO CONTROLADOR DE USER VALIDATE TOKEN");

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized();

            var token = authHeader["Bearer ".Length..].Trim();
            var result = await _externalAuthService.ValidateTokenAsync(token);

            return result.IsValid ? Ok(result) : Unauthorized();
        }
    }
}
