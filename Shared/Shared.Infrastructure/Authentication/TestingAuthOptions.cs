using Microsoft.AspNetCore.Authentication;

namespace Shared.Infrastructure.Authentication;

public class TestingAuthOptions : AuthenticationSchemeOptions
{
    // Valores por defecto para testing
    public string DefaultUserId { get; set; } = "test-user-123";
    public string DefaultUserEmail { get; set; } = "test@example.com";
    public string DefaultUserRoles { get; set; } = "User";

    // Headers opcionales para override en tests específicos
    public string UserIdHeader { get; set; } = "x-test-user-id";
    public string UserEmailHeader { get; set; } = "x-test-user-email";
    public string UserRolesHeader { get; set; } = "x-test-user-roles";
}