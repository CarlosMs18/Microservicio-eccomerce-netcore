using Microsoft.Extensions.Configuration;

namespace Cart.Infrastructure.Configuration
{
    public class EnvironmentConfigurationProvider
    {
        public static CartConfiguration GetConfiguration(IConfiguration config, string environment)
        {
            return environment switch
            {
                "Development" => GetDevelopmentConfig(config),
                "Docker" => GetDockerConfig(config),
                "Kubernetes" => GetKubernetesConfig(config),
                _ => throw new InvalidOperationException($"Entorno {environment} no soportado")
            };
        }

        private static CartConfiguration GetDevelopmentConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                ["database"] = config["Cart:DatabaseName"] ?? "CartDB_Dev",
                ["trusted"] = connectionParams["trusted"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CartConfiguration
            {
                Environment = "Development",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 5,
                    MaxRetryDelaySeconds = 30,
                    EnableDetailedErrors = true,
                    EnableSensitiveDataLogging = true
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Debug",
                    EnableFileLogging = true,
                    RetainedFileCountLimit = 15
                }
            };
        }

        private static CartConfiguration GetDockerConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "host.docker.internal,1433",
                ["database"] = config["Cart:DatabaseName"] ?? "CartDB_Dev",
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = connectionParams["password"] ?? throw new InvalidOperationException("Password requerido para Docker"),
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CartConfiguration
            {
                Environment = "Docker",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 10,
                    MaxRetryDelaySeconds = 60,
                    EnableDetailedErrors = false,
                    EnableSensitiveDataLogging = false
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Information",
                    EnableFileLogging = true,
                    RetainedFileCountLimit = 30
                }
            };
        }

        private static CartConfiguration GetKubernetesConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (string.IsNullOrEmpty(dbPassword))
            {
                throw new InvalidOperationException("Variable de entorno DB_PASSWORD no encontrada para Kubernetes");
            }

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? throw new InvalidOperationException("Server no configurado para Kubernetes"),
                ["database"] = config["Cart:DatabaseName"] ?? "CartDB_Dev",
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = dbPassword,
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "50", // Más conservador en K8s
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "2",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "60" // Más tiempo en K8s
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CartConfiguration
            {
                Environment = "Kubernetes",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 15,
                    MaxRetryDelaySeconds = 120,
                    EnableDetailedErrors = false,
                    EnableSensitiveDataLogging = false
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Information",
                    EnableFileLogging = true,
                    RetainedFileCountLimit = 7 // Menos archivos en K8s por espacio
                }
            };
        }

        private static string BuildConnectionString(string template, Dictionary<string, string> parameters)
        {
            var connectionString = parameters.Aggregate(template, (current, param) =>
                current.Replace($"{{{param.Key}}}", param.Value));

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Cadena de conexión no puede estar vacía");
            }

            return connectionString;
        }
    }

    // Clases de configuración
    public class CartConfiguration
    {
        public string Environment { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseConfiguration Database { get; set; } = new();
        public LoggingConfiguration Logging { get; set; } = new();
    }

    public class DatabaseConfiguration
    {
        public int MaxRetryCount { get; set; } = 5;
        public int MaxRetryDelaySeconds { get; set; } = 30;
        public bool EnableDetailedErrors { get; set; } = false;
        public bool EnableSensitiveDataLogging { get; set; } = false;
    }

    public class LoggingConfiguration
    {
        public string MinimumLevel { get; set; } = "Information";
        public bool EnableFileLogging { get; set; } = true;
        public int RetainedFileCountLimit { get; set; } = 15;
    }
}