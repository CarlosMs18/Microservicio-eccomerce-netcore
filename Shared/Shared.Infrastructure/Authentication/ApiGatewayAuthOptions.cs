using Microsoft.AspNetCore.Authentication;

namespace Shared.Infrastructure.Authentication;

public class ApiGatewayAuthOptions : AuthenticationSchemeOptions
{
    // Opciones adicionales si las necesitas en el futuro
    public string UserIdHeader { get; set; } = "x-user-id";
    public string UserEmailHeader { get; set; } = "x-user-email";
    public string UserRolesHeader { get; set; } = "x-user-roles";
}