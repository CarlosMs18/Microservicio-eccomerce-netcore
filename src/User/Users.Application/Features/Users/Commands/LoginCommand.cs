using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using User.Application.Contracts.Services;
using User.Application.DTOs.Requests;
using User.Application.DTOs.Responses;

using User.Application.Models;

namespace User.Application.Features.Users.Commands
{
    public class LoginCommand : IRequest<LoginResponse>
    {
        public LoginRequest Request { get; set; }
    }

    public class LoginHandlerCommand : IRequestHandler<LoginCommand, LoginResponse>
    {
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        
        public LoginHandlerCommand(IAuthService authService, UserManager<ApplicationUser> userManager)
        {
            _authService = authService;
            _userManager = userManager;
        }

        public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(request.Request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            var result = await _authService.PasswordSignInAsync(
                request.Request.Email,
                request.Request.Password,
                isPersistent: false,
                lockoutOnFailure: false
            );

            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            // Usa tu método original adaptado, ahora desde IAuthService
            var token = await _authService.GenerateJwtTokenAsync(user);

            return new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName
            };
        }
    }
}
