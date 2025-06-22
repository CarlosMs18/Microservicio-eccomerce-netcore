using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Authentication;

namespace Shared.Infrastructure.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiGatewayAuthentication(
        this IServiceCollection services)
    {
        // Solo para Kubernetes - autenticación basada en headers del API Gateway
        services.AddAuthentication("ApiGateway")
            .AddScheme<ApiGatewayAuthOptions, ApiGatewayAuthHandler>(
                "ApiGateway",
                options => { });
        return services;
    }

    public static IServiceCollection AddTestingAuthentication(
        this IServiceCollection services)
    {
        // Solo para Testing - bypass de autenticación con usuario fake
        services.AddAuthentication("Testing")
            .AddScheme<TestingAuthOptions, TestingAuthHandler>(
                "Testing",
                options =>
                {
                    // Configurar usuario por defecto para tests
                    options.DefaultUserId = "test-user-123";
                    options.DefaultUserEmail = "test@example.com";
                    options.DefaultUserRoles = "User,Admin";
                });
        return services;
    }
}