using Catalog.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Catalog.Infrastructure.HealthChecks
{
    public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly RabbitMQConfiguration _config;

        public RabbitMQHealthCheck(RabbitMQConfiguration config)
        {
            _config = config;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config.Host,
                    Port = _config.Port,
                    UserName = _config.Username,
                    Password = _config.Password,
                    VirtualHost = _config.VirtualHost,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                return await Task.FromResult(HealthCheckResult.Healthy("RabbitMQ is healthy"));
            }
            catch (Exception ex)
            {
                return await Task.FromResult(
                    HealthCheckResult.Unhealthy("RabbitMQ is unhealthy", ex));
            }
        }
    }
}