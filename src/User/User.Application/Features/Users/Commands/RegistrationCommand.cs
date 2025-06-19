using MediatR;
using Microsoft.AspNetCore.Identity;
using User.Application.Contracts.Persistence;
using User.Application.Contracts.Services;
using User.Application.DTOs.Requests;
using User.Application.DTOs.Responses;
using User.Application.Exceptions;
using User.Application.Models;



namespace User.Application.Features.Users.Commands
{
    public class RegistrationCommand : IRequest<RegistrationResponse>
    {
        public RegistrationRequest Request { get; set; } 
    }

    public class RegisterCommandHandler : IRequestHandler<RegistrationCommand, RegistrationResponse>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;

        public RegisterCommandHandler(UserManager<ApplicationUser> userManager, IUserRepository userRepository, IAuthService authService)
        {
            _userManager = userManager;
            _userRepository = userRepository;
            _authService = authService;
        }

        public async Task<RegistrationResponse> Handle(RegistrationCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;

            // 1. Validar email único
            if (await _userRepository.IsEmailUniqueAsync(request.Email) == false)
            {
                throw new BadRequestException($"El email {request.Email} ya está registrado.");
            }
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                EmailConfirmed = true 
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException($"Error al crear usuario: {errors}");
            }

            await _userManager.AddToRoleAsync(user, "User");
            var token = await _authService.GenerateJwtTokenAsync(user);

            return new RegistrationResponse
            {
                IsSuccess = true,
                UserId = user.Id,
                Message = "Registro exitoso",
                Token = token
            };
        }
    }
}
