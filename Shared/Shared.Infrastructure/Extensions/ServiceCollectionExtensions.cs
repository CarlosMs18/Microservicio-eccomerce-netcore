using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Resilience;

namespace Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        services.AddSingleton<IRepositoryResilience>(_ =>
            new RepositoryResilience(
                dbPolicy: DbPolicies.GetRetryPolicy(),
                httpRetry: HttpPolicies.GetRetryPolicy(),
                httpCircuitBreaker: HttpPolicies.GetCircuitBreaker()));

        return services;
    }
}