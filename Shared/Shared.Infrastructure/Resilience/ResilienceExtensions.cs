using Polly;
using Polly.Retry;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Interfaces;

namespace Shared.Infrastructure.Resilience;

public class RepositoryResilience : IRepositoryResilience
{
    public AsyncRetryPolicy DbRetryPolicy { get; }
    public IAsyncPolicy<HttpResponseMessage> HttpRetryPolicy { get; }
    public IAsyncPolicy<HttpResponseMessage> HttpCircuitBreaker { get; }

    public RepositoryResilience(AsyncRetryPolicy dbPolicy, IAsyncPolicy<HttpResponseMessage> httpRetry, IAsyncPolicy<HttpResponseMessage> httpCircuitBreaker)
    {
        DbRetryPolicy = dbPolicy;
        HttpRetryPolicy = httpRetry;
        HttpCircuitBreaker = httpCircuitBreaker;
    }
}