using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using System.Reflection;
using User.Application.Contracts.Persistence;
using User.Application.Contracts.Services;
using User.Application.Models;
using User.Infrastructure.Configuration;
using User.Infrastructure.Persistence;
using User.Infrastructure.Repositories;
using User.Infrastructure.Services.External.Grpc;
using User.Infrastructure.Services.Internal;
using User.Infrastructure.Services.External.Grpc.Interceptors;
using Grpc.AspNetCore.Server;
using Shared.Core.Interfaces;

namespace User.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            string environment)
        {
            // 1. Configuración centralizada
            var userConfig = EnvironmentConfigurationProvider.GetConfiguration(configuration, environment);
            services.AddSingleton(userConfig);

            // 2. Configuración de la base de datos
            ConfigureDatabase(services, userConfig, environment);

            // 3. Políticas de resiliencia
            services.AddResiliencePolicies();

            // 4. Registro de repositorios
            RegisterRepositories(services);

            // 5. Servicios de aplicación
            RegisterApplicationServices(services, configuration);

            // 6. Configuración de servicios gRPC
            ConfigureGrpcServices(services, configuration);

            return services;
        }

        private static void ConfigureDatabase(
            IServiceCollection services,
            UserConfiguration userConfig,
            string environment)
        {
            services.AddDbContext<UserIdentityDbContext>((provider, options) =>
            {
                var logger = provider.GetRequiredService<ILogger<UserIdentityDbContext>>();

                options.UseSqlServer(userConfig.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: userConfig.Database.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(userConfig.Database.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                });

                if (environment == "Development")
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                    logger.LogDebug("🔍 Habilitados logs detallados de EF Core");
                }
            });
        }

        private static void RegisterRepositories(IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork>(provider =>
                new UnitOfWork(
                    provider.GetRequiredService<UserIdentityDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>(),
                    provider.GetRequiredService<IUserRepository>()));

            services.AddScoped<IUserRepository>(provider =>
                new UserRepository(
                    provider.GetRequiredService<UserIdentityDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
        }

        private static void RegisterApplicationServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Configuración de Identity
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                ConfigureIdentityOptions(options);
            })
            .AddEntityFrameworkStores<UserIdentityDbContext>()
            .AddDefaultTokenProviders();

            // Configuración JWT
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // Servicios de aplicación
            services.AddScopedServices();

            // AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
        }

        private static void ConfigureGrpcServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors", true);
                options.MaxReceiveMessageSize = configuration.GetValue<int>("Grpc:MaxMessageSizeMB", 16) * 1024 * 1024;
                options.IgnoreUnknownServices = true;
                options.Interceptors.Add<ExceptionInterceptor>();
            });

            services.AddScoped<AuthGrpcService>();
            services.AddSingleton<ExceptionInterceptor>();

            services.Configure<GrpcServiceOptions>(options =>
            {
                options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors", true);
                options.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            });
        }

        private static void ConfigureIdentityOptions(IdentityOptions options)
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8,
                RequireNonAlphanumeric = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true
            };

            options.User = new UserOptions
            {
                RequireUniqueEmail = true,
                AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+"
            };

            options.Lockout = new LockoutOptions
            {
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)
            };
        }
    }

    // Extensiones adicionales para mejor organización
    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddScopedServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IAuthService, AuthService>()
                .AddScoped<IHealthChecker, HealthChecker>();
        }
    }
}