using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Core.Interfaces;
using User.Application.Models;
using User.Infrastructure.Configuration;
using User.Infrastructure.Persistence;

namespace User.IntegrationTests.Fixtures
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        public Mock<IExternalAuthService> MockExternalAuthService { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // 🌍 Detectar entorno dinámicamente (igual que en Program.cs)
            var environment = DetectEnvironment();
            builder.UseEnvironment(environment);

            // 📋 Configurar archivos de configuración según el entorno detectado
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{environment}.json", optional: true)
                      .AddEnvironmentVariables();
            });

            builder.ConfigureServices(services =>
            {
                // 🧪 Establecer variable de ambiente según el entorno
                if (environment == "Testing")
                {
                    Environment.SetEnvironmentVariable("ASPNETCORE_TESTING", "true");
                }

                // 🗄️ CONFIGURAR BASE DE DATOS DE TEST

                // 1. Remover el DbContext real
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<UserIdentityDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // 2. Remover configuración original de UserConfiguration
                var configDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(UserConfiguration));
                if (configDescriptor != null)
                    services.Remove(configDescriptor);

                // 3. Configurar BD según el entorno detectado
                ConfigureDatabaseForEnvironment(services, environment);

                // 4. Asegurarse de que Identity esté configurado correctamente
                ConfigureIdentityForTesting(services, environment);

                // 🎭 CONFIGURAR MOCKS
                services.RemoveAll<IExternalAuthService>();
                MockExternalAuthService = new Mock<IExternalAuthService>();
                services.AddSingleton(MockExternalAuthService.Object);

                // 📝 CONFIGURAR LOGGING según el entorno
                ConfigureLoggingForEnvironment(services, environment);
            });
        }

        private static string DetectEnvironment()
        {
            // Detectar CI/CD primero
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                return "CI";

            // Detectar otros entornos containerizados
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
                return "Kubernetes";

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
                return "Docker";

            // Por defecto para tests locales
            return "Testing";
        }

        private static void ConfigureDatabaseForEnvironment(IServiceCollection services, string environment)
        {
            // Crear configuración temporal para obtener connection string
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Obtener configuración según el entorno
            var config = EnvironmentConfigurationProvider.GetConfiguration(configuration, environment);

            // Registrar configuración
            services.AddSingleton(config);

            // Log para debugging
            Console.WriteLine($"🔧 Configurando BD para entorno: {environment}");
            Console.WriteLine($"🔗 Connection String: {MaskConnectionString(config.ConnectionString)}");

            // Configurar DbContext
            services.AddDbContext<UserIdentityDbContext>((serviceProvider, options) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<UserIdentityDbContext>>();

                options.UseSqlServer(config.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: config.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(config.Database.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                });

                // Configurar según el entorno
                if (config.Database.EnableDetailedErrors)
                {
                    options.EnableDetailedErrors();
                }

                if (config.Database.EnableSensitiveDataLogging)
                {
                    options.EnableSensitiveDataLogging();
                }

                logger.LogDebug("🧪 Configurando BD para {Environment}: {ConnectionString}",
                    environment, MaskConnectionString(config.ConnectionString));
            });
        }

        private static void ConfigureIdentityForTesting(IServiceCollection services, string environment)
        {
            // Configurar Identity según el entorno
            services.Configure<IdentityOptions>(options =>
            {
                if (environment == "Testing")
                {
                    // Password requirements más simples para tests locales
                    options.Password.RequiredLength = 6;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;

                    // Lockout más permisivo para tests
                    options.Lockout.MaxFailedAccessAttempts = 10;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
                }
                else if (environment == "CI")
                {
                    // Password requirements normales para CI
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;

                    // Lockout más permisivo para CI (evitar fallos por timing)
                    options.Lockout.MaxFailedAccessAttempts = 10;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
                }

                // Común para todos los entornos de test
                options.User.RequireUniqueEmail = true;
            });
        }

        private static void ConfigureLoggingForEnvironment(IServiceCollection services, string environment)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();

                // Nivel de logging según el entorno
                var minLevel = environment switch
                {
                    "CI" => LogLevel.Information,  // Más logs en CI para debugging
                    "Testing" => LogLevel.Warning, // Menos logs en tests locales
                    _ => LogLevel.Warning
                };

                builder.SetMinimumLevel(minLevel);

                Console.WriteLine($"📝 Configurando logging para {environment} con nivel mínimo: {minLevel}");
            });
        }

        // Método helper para enmascarar datos sensibles en logs
        private static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "N/A";

            // Enmascarar password si existe
            var masked = connectionString;
            if (connectionString.Contains("Password="))
            {
                var regex = new System.Text.RegularExpressions.Regex(@"Password=([^;]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                masked = regex.Replace(connectionString, "Password=***");
            }

            return masked;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 🧹 Limpiar recursos
                MockExternalAuthService?.Reset();
            }
            base.Dispose(disposing);
        }

        // 🛠️ MÉTODO HELPER PARA INICIALIZAR BD EN TESTS
        public async Task<UserIdentityDbContext> GetDbContextAsync()
        {
            var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();

            // Asegurar que la BD esté creada y migrada
            await context.Database.EnsureCreatedAsync();

            return context;
        }

        // 🛠️ MÉTODO HELPER PARA LIMPIAR BD ENTRE TESTS
        public async Task CleanDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();

            try
            {
                // Limpiar tablas en orden correcto (FK constraints)
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserRoles");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUsers");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetRoles");

                Console.WriteLine("🧹 Base de datos limpiada correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error al limpiar BD: {ex.Message}");
                // En caso de error, recrear la BD
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }
        }

        // 🛠️ MÉTODO HELPER PARA SEEDEAR DATOS DE TEST
        public async Task SeedTestDataAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            // Asegurar que la BD esté creada
            await context.Database.EnsureCreatedAsync();

            // Crear roles por defecto
            await EnsureRoleExistsAsync(roleManager, "User");
            await EnsureRoleExistsAsync(roleManager, "Admin");

            Console.WriteLine("🌱 Datos de test seeded correctamente");
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<ApplicationRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                });
            }
        }

        // 🛠️ MÉTODO HELPER PARA CREAR USUARIOS DE TEST
        public async Task<ApplicationUser> CreateTestUserAsync(string email, string password, params string[] roles)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User"
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Asignar roles si se especifican
            if (roles.Length > 0)
            {
                await userManager.AddToRolesAsync(user, roles);
            }

            Console.WriteLine($"👤 Usuario de test creado: {email}");
            return user;
        }
    }
}