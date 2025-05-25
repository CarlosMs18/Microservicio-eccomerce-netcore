using Polly;
using Shared.Core.Constants;

namespace Shared.Infrastructure.Resilience;

public static class HttpPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetries = ResilienceConstants.MaxHttpRetries)
        => Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreaker(int exceptionsBeforeBreak = ResilienceConstants.CircuitBreakerThreshold)
        => Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsBeforeBreak,
                TimeSpan.FromMinutes(1));
}