using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cart.Infrastructure.Extensions
{
    public class RabbitMQConfiguration
    {
        public const string SectionName = "RabbitMQ";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string ConnectionString { get; set; } = string.Empty;

        // Configuraciones adicionales
        public bool AutomaticRecoveryEnabled { get; set; } = true;
        public int NetworkRecoveryIntervalSeconds { get; set; } = 10;
        public int RequestedHeartbeatSeconds { get; set; } = 60;

        /// <summary>
        /// Construye la configuración de RabbitMQ siguiendo el mismo patrón que ConnectionString
        /// </summary>
        public static RabbitMQConfiguration BuildFromConfiguration(IConfiguration configuration, string environment)
        {
            try
            {
                var config = new RabbitMQConfiguration();

                // 1. Cargar valores base desde "RabbitMQ" section
                configuration.GetSection(SectionName).Bind(config);

                // 2. Sobreescribir con parámetros específicos del entorno
                var rabbitMQParams = configuration.GetSection("RabbitMQParameters");
                if (rabbitMQParams.Exists())
                {
                    config.Host = rabbitMQParams["host"] ?? config.Host;
                    config.Port = rabbitMQParams.GetValue<int>("port", config.Port);
                    config.Username = rabbitMQParams["username"] ?? config.Username;
                    config.Password = rabbitMQParams["password"] ?? config.Password;
                    config.VirtualHost = rabbitMQParams["virtualhost"] ?? config.VirtualHost;
                }

                // 3. Para Kubernetes, obtener password de variables de entorno
                if (environment == "Kubernetes")
                {
                    var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");
                    if (!string.IsNullOrEmpty(rabbitPassword))
                    {
                        config.Password = rabbitPassword;
                    }
                }

                // 4. Construir ConnectionString dinámicamente
                config.ConnectionString = BuildConnectionString(configuration, config);

                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al construir configuración RabbitMQ para {environment}: {ex.Message}", ex);
            }
        }

        private static string BuildConnectionString(IConfiguration configuration, RabbitMQConfiguration config)
        {
            var template = configuration.GetValue<string>("RabbitMQTemplates:Default")
                          ?? "amqp://{username}:{password}@{host}:{port}/{virtualhost}";

            var virtualHost = config.VirtualHost == "/" ? "" : config.VirtualHost;

            return template
                .Replace("{username}", config.Username)
                .Replace("{password}", config.Password)
                .Replace("{host}", config.Host)
                .Replace("{port}", config.Port.ToString())
                .Replace("{virtualhost}", virtualHost);
        }
    }
}
