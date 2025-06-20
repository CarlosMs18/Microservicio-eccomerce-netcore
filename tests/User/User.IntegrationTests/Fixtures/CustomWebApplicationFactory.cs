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
            // 🌍 Detectar entorno simple: solo CI o Testing
            var environment = DetectEnvironment();
            builder.UseEnvironment(environment);

            // 📋 Configurar archivos de configuración
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{environment}.json", optional: true)
                      .AddEnvironmentVariables();
            });

            builder.ConfigureServices(services =>
            {
                // 🧪 Variable de ambiente para testing
                Environment.SetEnvironmentVariable("ASPNETCORE_TESTING", "true");

                // 🗄️ Configurar base de datos de test
                ConfigureDatabaseForTesting(services, environment);

                // 🛡️ Configurar Identity para testing
                ConfigureIdentityForTesting(services);

                // 🎭 Configurar mocks
                services.RemoveAll<IExternalAuthService>();
                MockExternalAuthService = new Mock<IExternalAuthService>();
                services.AddSingleton(MockExternalAuthService.Object);

                // 📝 Configurar logging simple
                ConfigureLogging(services, environment);
            });
        }

        private static string DetectEnvironment()
        {
            // Solo CI o Testing - simple y directo
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ? "CI" : "Testing";
        }

        private static void ConfigureDatabaseForTesting(IServiceCollection services, string environment)
        {
            // Remover configuraciones existentes
            services.RemoveAll<DbContextOptions<UserIdentityDbContext>>();
            services.RemoveAll<UserConfiguration>();

            // Crear configuración temporal
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Obtener configuración según entorno
            var config = EnvironmentConfigurationProvider.GetConfiguration(configuration, environment);
            services.AddSingleton(config);

            // Configurar DbContext
            services.AddDbContext<UserIdentityDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(config.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: config.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(config.Database.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                });

                // Configuraciones para testing
                if (config.Database.EnableDetailedErrors)
                    options.EnableDetailedErrors();

                if (config.Database.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();
            });
        }

        private static void ConfigureIdentityForTesting(IServiceCollection services)
        {
            services.Configure<IdentityOptions>(options =>
            {
                // Password más simple para tests
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;

                // Usuario único por email
                options.User.RequireUniqueEmail = true;

                // Lockout permisivo para tests
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
            });
        }

        private static void ConfigureLogging(IServiceCollection services, string environment)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();

                // Solo Warning para Testing, Information para CI
                var level = environment == "CI" ? LogLevel.Information : LogLevel.Warning;
                builder.SetMinimumLevel(level);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MockExternalAuthService?.Reset();
            }
            base.Dispose(disposing);
        }

        // 🛠️ HELPERS PARA TESTS

        public async Task<UserIdentityDbContext> GetDbContextAsync()
        {
            var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        public async Task CleanDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();

            try
            {
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserRoles");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUsers");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetRoles");
            }
            catch (Exception)
            {
                // Si falla, recrear la BD
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }
        }

        public async Task SeedTestDataAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            await context.Database.EnsureCreatedAsync();

            // Crear roles básicos
            await EnsureRoleExistsAsync(roleManager, "User");
            await EnsureRoleExistsAsync(roleManager, "Admin");
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

            if (roles.Length > 0)
            {
                await userManager.AddToRolesAsync(user, roles);
            }

            return user;
        }
    }
}