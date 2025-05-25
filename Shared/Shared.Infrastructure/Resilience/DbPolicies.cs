using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;
using Shared.Core.Constants;

namespace Shared.Infrastructure.Resilience;

public static class DbPolicies
{
    public static AsyncRetryPolicy GetRetryPolicy(int maxRetries = ResilienceConstants.MaxDatabaseRetries)
        => Policy
            .Handle<SqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}