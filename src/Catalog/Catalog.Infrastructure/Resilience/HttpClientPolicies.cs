using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace Catalog.Infrastructure.Resilience
{
    public static class HttpClientPolicies
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IConfiguration config)
        {
            int retryCount = config.GetValue<int>("HttpClientPolicies:RetryCount");
            double retryDelaySec = config.GetValue<double>("HttpClientPolicies:RetryDelaySec");

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount,
                    attempt => TimeSpan.FromSeconds(retryDelaySec * Math.Pow(2, attempt)) // Backoff exponencial
                );
        }

        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(IConfiguration config)
        {
            int failures = config.GetValue<int>("HttpClientPolicies:CircuitBreakerFailures");
            int durationSec = config.GetValue<int>("HttpClientPolicies:CircuitBreakerDurationSec");

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: failures,
                    durationOfBreak: TimeSpan.FromSeconds(durationSec)
                );
        }
    }
}
