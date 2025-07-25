using Microsoft.Extensions.Configuration;

namespace User.Infrastructure.Configuration
{
    public class EnvironmentConfigurationProvider
    {
        public static UserConfiguration GetConfiguration(IConfiguration config, string environment)
        {
            return environment switch
            {
                "Production" => GetProductionConfig(config),
                "Development" => GetDevelopmentConfig(config),
                "Docker" => GetDockerConfig(config),
                "Kubernetes" => GetKubernetesConfig(config),
                "Testing" => GetTestingConfig(config),
                "CI" => GetCIConfig(config),
                _ => throw new InvalidOperationException($"Entorno {environment} no soportado")
            };
        }
        private static UserConfiguration GetProductionConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Azure"] ?? throw new InvalidOperationException("Template Azure no encontrado");

            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (string.IsNullOrEmpty(dbPassword))
            {
                throw new InvalidOperationException("Variable de entorno DB_PASSWORD no encontrada para Production");
            }

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? throw new InvalidOperationException("Server no configurado para Production"),
                ["database"] = config["User:DatabaseName"] ?? "microservices-db", // ← BD de producción
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = dbPassword,
                ["trust"] = connectionParams["trust"] ?? "true",
                ["encrypt"] = connectionParams["encrypt"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100", // Más conexiones en prod
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "45"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new UserConfiguration
            {
                Environment = "Production",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 10,
                    MaxRetryDelaySeconds = 60,
                    EnableDetailedErrors = false,      // ← Seguridad en prod
                    EnableSensitiveDataLogging = false // ← Nunca en prod
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Warning",          // ← Solo warnings/errors
                    EnableFileLogging = true,
                    RetainedFileCountLimit = 30        // ← Más logs en prod
                },
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 3,       // ← Más estricto
                    LockoutTimeSpanMinutes = 60        // ← Lockout más largo
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false,      // ← Sin detalles en prod
                    MaxMessageSizeMB = 8,              // ← Más conservador
                    EnableCompression = true
                }
            };
        }

        private static UserConfiguration GetDevelopmentConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                ["database"] = config["User:DatabaseName"] ?? "UserDB_Dev",
                ["encrypt"] = connectionParams["encrypt"] ?? "false",
                ["trusted"] = connectionParams["trusted"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new UserConfiguration
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
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 5,
                    LockoutTimeSpanMinutes = 15
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true,
                    MaxMessageSizeMB = 16,
                    EnableCompression = true
                }
            };
        }


        private static UserConfiguration GetCIConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "localhost,1433",
                ["database"] = config["User:DatabaseName"] ?? "UserDB_Testing",
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = connectionParams["password"] ?? "P@ssw0rd123!",
                ["encrypt"] = connectionParams["encrypt"] ?? "false",
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "50",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "2",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "60"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new UserConfiguration
            {
                Environment = "CI",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 5,
                    MaxRetryDelaySeconds = 30,
                    EnableDetailedErrors = true, // Habilitado para debugging en CI
                    EnableSensitiveDataLogging = false // Deshabilitado por seguridad
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Information",
                    EnableFileLogging = false, // Sin archivos en CI
                    RetainedFileCountLimit = 0
                },
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 10, // Más permisivo para tests
                    LockoutTimeSpanMinutes = 1 // Lockout corto para tests
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true,
                    MaxMessageSizeMB = 16,
                    EnableCompression = false // Sin compresión en CI para simplicidad
                }
            };
        }

        private static UserConfiguration GetTestingConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");
            var template = templates["Local"] ?? throw new InvalidOperationException("Template Local no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "(localdb)\\mssqllocaldb",
                ["database"] = config["User:DatabaseName"] ?? "UserDB_Testing", // 👈 BD diferente para tests
                ["encrypt"] = connectionParams["encrypt"] ?? "false",
                ["trusted"] = connectionParams["trusted"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new UserConfiguration
            {
                Environment = "Testing",
                ConnectionString = connectionString,
                Database = new DatabaseConfiguration
                {
                    MaxRetryCount = 3, // 👈 Menos reintentos en tests
                    MaxRetryDelaySeconds = 10, // 👈 Menos delay en tests
                    EnableDetailedErrors = true,
                    EnableSensitiveDataLogging = true
                },
                Logging = new LoggingConfiguration
                {
                    MinimumLevel = "Warning", // 👈 Menos logs en tests
                    EnableFileLogging = false, // 👈 Sin archivos en tests
                    RetainedFileCountLimit = 0
                },
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 10, // 👈 Más permisivo en tests
                    LockoutTimeSpanMinutes = 1 // 👈 Lockout más corto en tests
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = true,
                    MaxMessageSizeMB = 16,
                    EnableCompression = false // 👈 Sin compresión en tests
                }
            };
        }

        private static UserConfiguration GetDockerConfig(IConfiguration config)
        {
            var connectionParams = config.GetSection("ConnectionParameters");
            var poolingParams = config.GetSection("ConnectionPooling");
            var templates = config.GetSection("ConnectionTemplates");

            var template = templates["Remote"] ?? throw new InvalidOperationException("Template Remote no encontrado");

            var parameters = new Dictionary<string, string>
            {
                ["server"] = connectionParams["server"] ?? "host.docker.internal,1433",
                ["database"] = config["User:DatabaseName"] ?? "UserDB_Dev",
                ["user"] = connectionParams["user"] ?? "sa",
                ["password"] = connectionParams["password"] ?? throw new InvalidOperationException("Password requerido para Docker"),
                ["encrypt"] = connectionParams["encrypt"] ?? "false",
                ["trust"] = connectionParams["trust"] ?? "true",
                ["pooling"] = poolingParams["pooling"] ?? "true",
                ["maxPoolSize"] = poolingParams["maxPoolSize"] ?? "100",
                ["minPoolSize"] = poolingParams["minPoolSize"] ?? "5",
                ["connectionTimeout"] = poolingParams["connectionTimeout"] ?? "30",
                ["commandTimeout"] = poolingParams["commandTimeout"] ?? "30"
            };

            var connectionString = BuildConnectionString(template, parameters);

            return new UserConfiguration
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
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 5,
                    LockoutTimeSpanMinutes = 15
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false,
                    MaxMessageSizeMB = 16,
                    EnableCompression = true
                }
            };
        }

        private static UserConfiguration GetKubernetesConfig(IConfiguration config)
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
                ["database"] = config["User:DatabaseName"] ?? "UserDB_Dev",
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

            return new UserConfiguration
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
                Identity = new IdentityConfiguration
                {
                    RequireUniqueEmail = true,
                    MaxFailedAccessAttempts = 3, // Más estricto en producción
                    LockoutTimeSpanMinutes = 30
                },
                Grpc = new GrpcConfiguration
                {
                    EnableDetailedErrors = false,
                    MaxMessageSizeMB = 8, // Más conservador en K8s
                    EnableCompression = true
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
    public class UserConfiguration
    {
        public string Environment { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseConfiguration Database { get; set; } = new();
        public LoggingConfiguration Logging { get; set; } = new();
        public IdentityConfiguration Identity { get; set; } = new();
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

    public class IdentityConfiguration
    {
        public bool RequireUniqueEmail { get; set; } = true;
        public int MaxFailedAccessAttempts { get; set; } = 5;
        public int LockoutTimeSpanMinutes { get; set; } = 15;
    }

    public class GrpcConfiguration
    {
        public bool EnableDetailedErrors { get; set; } = false;
        public int MaxMessageSizeMB { get; set; } = 16;
        public bool EnableCompression { get; set; } = true;
    }
}