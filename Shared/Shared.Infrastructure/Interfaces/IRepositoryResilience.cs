using Polly;
using Polly.Retry;

namespace Shared.Infrastructure.Interfaces
{
    public interface IRepositoryResilience
    {
        AsyncRetryPolicy DbRetryPolicy { get; }
        IAsyncPolicy<HttpResponseMessage> HttpRetryPolicy { get; }
        IAsyncPolicy<HttpResponseMessage> HttpCircuitBreaker { get; }
    }
}
