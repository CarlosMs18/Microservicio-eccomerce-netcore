using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Application.Contracts.Messaging;
using Catalog.Infrastructure.Configuration;
using Catalog.Infrastructure.HealthChecks;
using Catalog.Infrastructure.Services.External.Messaging;

namespace Catalog.Infrastructure.Extensions
{
    public static class MessagingExtensions
    {
        public static IServiceCollection AddRabbitMQMessaging(
            this IServiceCollection services,
            IConfiguration configuration,
            string environment) // ✅ Agregar el parámetro environment
        {
            // Registrar configuración usando el método dinámico que ya tienes
            var rabbitMQConfig = RabbitMQConfiguration.BuildFromConfiguration(configuration, environment);
            services.AddSingleton(rabbitMQConfig);

            // Registrar el EventPublisher
            services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();

            // Agregar Health Check
            services.AddHealthChecks()
                .AddCheck<RabbitMQHealthCheck>("rabbitmq", tags: new[] { "messaging", "rabbitmq" });

            return services;
        }
    }
}