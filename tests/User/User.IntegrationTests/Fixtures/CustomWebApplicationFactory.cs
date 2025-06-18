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
            builder.ConfigureServices(services =>
            {
                // 🧪 Establecer variable de ambiente para Testing
                Environment.SetEnvironmentVariable("ASPNETCORE_TESTING", "true");

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

                // 3. Configurar BD para Testing
                ConfigureDatabaseForTesting(services);

                // 4. Asegurarse de que Identity esté configurado correctamente
                ConfigureIdentityForTesting(services);

                // 🎭 CONFIGURAR MOCKS
                services.RemoveAll<IExternalAuthService>();
                MockExternalAuthService = new Mock<IExternalAuthService>();
                services.AddSingleton(MockExternalAuthService.Object);

                // 📝 CONFIGURAR LOGGING
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            });

            // 🌍 Configurar entorno de testing
            builder.UseEnvironment("Testing");

            // 📋 Configurar archivos de configuración
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false)
                      .AddJsonFile("appsettings.Testing.json", optional: false)
                      .AddEnvironmentVariables();
            });
        }

        private static void ConfigureDatabaseForTesting(IServiceCollection services)
        {
            // Crear configuración temporal para obtener connection string
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Testing.json", optional: false)
                .Build();

            // Obtener configuración de testing
            var testConfig = EnvironmentConfigurationProvider.GetConfiguration(configuration, "Testing");

            // Registrar configuración de testing
            services.AddSingleton(testConfig);

            // Configurar DbContext con SQL Server LocalDB
            services.AddDbContext<UserIdentityDbContext>((serviceProvider, options) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<UserIdentityDbContext>>();

                options.UseSqlServer(testConfig.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: testConfig.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(testConfig.Database.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                });

                // Configurar para testing
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();

                logger.LogDebug("🧪 Configurando BD para Testing: {ConnectionString}",
                    testConfig.ConnectionString);
            });
        }

        private static void ConfigureIdentityForTesting(IServiceCollection services)
        {
            // Configurar Identity para tests (más permisivo)
            services.Configure<IdentityOptions>(options =>
            {
                // Password requirements más simples para tests
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;

                // User requirements
                options.User.RequireUniqueEmail = true;

                // Lockout más permisivo para tests
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
            });
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

            // Limpiar tablas en orden correcto (FK constraints)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserRoles");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUsers");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM AspNetRoles");
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

            return user;
        }
    }
}