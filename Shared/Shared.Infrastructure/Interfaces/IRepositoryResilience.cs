using Polly;
using Polly.Retry;

namespace Shared.Infrastructure.Interfaces
{
    public interface IRepositoryResilience
    {
        AsyncRetryPolicy DbRetryPolicy { get; }
        IAsyncPolicy<HttpResponseMessage> HttpRetryPolicy { get; } // Ya está correctamente tipado
        IAsyncPolicy<HttpResponseMessage> HttpCircuitBreaker { get; } // Ya está correctamente tipado
    }
}
