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
}