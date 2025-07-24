using Microsoft.Extensions.Configuration;

namespace Catalog.Infrastructure.Configuration
{
    public class EnvironmentConfigurationProvider
    {
        public static CatalogConfiguration GetConfiguration(IConfiguration config, string environment)
        {
            return environment switch
            {
                "Development" => GetDevelopmentConfig(config),
                "Testing" => GetTestingConfig(config),
                "Docker" => GetDockerConfig(config),
                "CI" => GetCIConfig(config),
                "Kubernetes" => GetKubernetesConfig(config),
                "Production" => GetProductionConfig(config),
                _ => throw new InvalidOperationException($"Entorno {environment} no soportado")
            };
        }

        private static CatalogConfiguration GetDevelopmentConfig(IConfiguration config)
        {
            Console.WriteLine("GetDevelopmentConfig");
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
                ["trusted"] = connectionParams["trusted"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CatalogConfiguration
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
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true,
                    MaxMessageSizeMB = 4,
                    CompressionLevel = "Optimal"
                }
            };
        }

        private static CatalogConfiguration GetTestingConfig(IConfiguration config)
        {
            Console.WriteLine("GetTestingConfig");
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            // Usa el template Local ya que Testing también usa LocalDB
            var template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_Test",
                ["trusted"] = connectionParams["trusted"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "10", // Más restrictivo para testing
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "1",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "10", // Timeouts más cortos
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "10"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CatalogConfiguration
            {
                Environment = "Testing",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 3, // Menos reintentos en testing
                    MaxRetryDelaySeconds = 10, // Delays más cortos
                    EnableDetailedErrors = true, // Útil para debugging de tests
                    EnableSensitiveDataLogging = false // Por seguridad en tests
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Debug", // Solo warnings y errores en tests
                    EnableFileLogging = false, // Sin archivos de log en testing
                    RetainedFileCountLimit = 5 // Mínimo si se habilita file logging
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true, // Útil para debugging
                    MaxMessageSizeMB = 2, // Más restrictivo para tests
                    CompressionLevel = "Fastest" // Velocidad sobre compresión en tests
                }
            };
        }

        private static CatalogConfiguration GetCIConfig(IConfiguration config)
        {
            Console.WriteLine("GetCIConfig");
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            // Usa el template Remote ya que CI usa SQL Server en contenedor
            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "localhost,1433",
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_CI",
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = connectionParams["password"] ?? "P@ssw0rd123!",
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "5", // Más restrictivo para CI
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "1",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30", // Timeouts más generosos para CI
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CatalogConfiguration
            {
                Environment = "CI",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 5, // Reintentos moderados en CI
                    MaxRetryDelaySeconds = 30, // Delays generosos para CI
                    EnableDetailedErrors = true, // Útil para debugging en CI
                    EnableSensitiveDataLogging = false // Por seguridad en CI
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Information", // Information level para CI
                    EnableFileLogging = false, // Sin archivos de log en CI
                    RetainedFileCountLimit = 5 // Mínimo si se habilita file logging
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true, // Útil para debugging en CI
                    MaxMessageSizeMB = 2, // Más restrictivo para CI
                    CompressionLevel = "Fastest" // Velocidad sobre compresión en CI
                }
            };
        }

        private static CatalogConfiguration GetDockerConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "host.docker.internal,1433",
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
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

            return new CatalogConfiguration
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
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false,
                    MaxMessageSizeMB = 4,
                    CompressionLevel = "Optimal"
                }
            };
        }

        private static CatalogConfiguration GetKubernetesConfig(IConfiguration config)
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
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_Dev",
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

            return new CatalogConfiguration
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
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false,
                    MaxMessageSizeMB = 2, // Más conservador en K8s
                    CompressionLevel = "Fastest"
                }
            };
        }

        private static CatalogConfiguration GetProductionConfig(IConfiguration config)
        {
            Console.WriteLine("GetProductionConfig");
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (string.IsNullOrEmpty(dbPassword))
            {
                throw new InvalidOperationException("Variable de entorno DB_PASSWORD no encontrada para Production");
            }

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? throw new InvalidOperationException("Server no configurado para Production"),
                ["database"] = config["Catalog:DatabaseName"] ?? "CatalogDB_Prod", // ← BD de producción
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = dbPassword,
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100", // Más conexiones en prod
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "45" // Más tiempo para comandos complejos
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new CatalogConfiguration
            {
                Environment = "Production",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 10, // Más reintentos en prod
                    MaxRetryDelaySeconds = 60, // Delays más largos
                    EnableDetailedErrors = false, // ← Seguridad en prod
                    EnableSensitiveDataLogging = false // ← Nunca en prod
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Warning", // ← Solo warnings/errors
                    EnableFileLogging = true,
                    RetainedFileCountLimit = 30 // ← Más logs en prod
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false, // ← Sin detalles en prod
                    MaxMessageSizeMB = 8, // ← Más conservador pero funcional
                    CompressionLevel = "Optimal" // Mejor compresión en prod
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
    public class CatalogConfiguration
    {
        public string Environment { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseConfiguration Database { get; set; } = new();
        public LoggingConfiguration Logging { get; set; } = new();
        public GrpcConfiguration Grpc { get; set; } = new();
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

    public class GrpcConfiguration
    {
        public bool EnableDetailedErrors { get; set; } = false;
        public int MaxMessageSizeMB { get; set; } = 4;
        public string CompressionLevel { get; set; } = "Optimal";
    }
}