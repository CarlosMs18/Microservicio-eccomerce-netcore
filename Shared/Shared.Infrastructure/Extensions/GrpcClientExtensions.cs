using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net.Http;

namespace Shared.Infrastructure.Extensions
{
    /// <summary>
    /// Extensiones genéricas para clientes gRPC - NO conoce protos específicos
    /// </summary>
    public static class GrpcClientExtensions
    {
        /// <summary>
        /// Método genérico para agregar cualquier cliente gRPC con configuración estándar
        /// </summary>
        public static IServiceCollection AddGrpcClientWithResilience<TClient>(
            this IServiceCollection services,
            string serviceUrl,
            GrpcServiceSettings? settings = null,
            Action<GrpcClientFactoryOptions>? configureClient = null,
            Action<GrpcChannelOptions>? configureChannel = null)
            where TClient : class
        {
            settings ??= new GrpcServiceSettings();

            services.AddGrpcClient<TClient>(options =>
            {
                options.Address = new Uri(serviceUrl);
                configureClient?.Invoke(options);
            })
            .ConfigureChannel(channelOptions =>
            {
                channelOptions.HttpHandler = new SocketsHttpHandler
                {
                    PooledConnectionIdleTimeout = settings.EnableKeepAlive
                        ? Timeout.InfiniteTimeSpan
                        : TimeSpan.FromMinutes(2),
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    EnableMultipleHttp2Connections = true
                };
                configureChannel?.Invoke(channelOptions);
            })
            .AddPolicyHandler(CreateRetryPolicy(settings.RetryCount));

            return services;
        }

        /// <summary>
        /// Helper para construir URLs gRPC desde template
        /// </summary>
        public static string BuildGrpcUrl(string template, string host, string port)
        {
            return template
                .Replace("{host}", host)
                .Replace("{port}", port);
        }

        private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int retryCount)
        {
            return Policy<HttpResponseMessage>
                .Handle<RpcException>(e => e.StatusCode is
                    StatusCode.Unavailable or
                    StatusCode.DeadlineExceeded or
                    StatusCode.ResourceExhausted)
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"Reintentando llamada gRPC. Intento: {retryCount}, Delay: {timespan}");
                    });
        }
    }

    /// <summary>
    /// Configuración genérica para servicios gRPC
    /// </summary>
    public class GrpcServiceSettings
    {
        public string Host { get; set; } = "localhost";
        public string Port { get; set; } = "5000";
        public string Protocol { get; set; } = "http";
        public int RetryCount { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableKeepAlive { get; set; } = true;
    }
}