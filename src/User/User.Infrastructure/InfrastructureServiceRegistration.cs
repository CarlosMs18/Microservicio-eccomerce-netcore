using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using System;
using System.Net.Http;
using System.Reflection;
using User.Application.Contracts.Persistence;
using User.Application.Contracts.Services;
using User.Application.Models;
using User.Infrastructure.Persistence;
using User.Infrastructure.Repositories;
using User.Infrastructure.Services.External.Grpc;
using User.Infrastructure.Services.Internal;

namespace User.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configuración inicial con logging estructurado
            var logger = InitializeLogging(services, configuration);

            // Configuración de base de datos con detección de entorno
            ConfigureDatabase(services, configuration, logger);

            // Configuración de resiliencia (usando tu implementación existente en Shared)
            services.AddResiliencePolicies();

            // Registro de servicios
            RegisterApplicationServices(services, configuration, logger);

            logger.LogInformation("Infrastructure services configured successfully");
            return services;
        }

        private static ILogger InitializeLogging(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(configure =>
            {
                configure.AddConsole()
                        .AddConfiguration(configuration.GetSection("Logging"))
                        .AddJsonConsole(); // Para logs estructurados
            });

            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger("InfrastructureServiceRegistration");
        }

        private static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration, ILogger logger)
        {
            var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
            var connectionString = GetConnectionString(configuration, isKubernetes, logger);

            services.AddDbContext<UserIdentityDbContext>((provider, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });

                // Configuración según entorno
                if (!provider.GetRequiredService<IHostEnvironment>().IsProduction())
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });
        }

        private static string GetConnectionString(IConfiguration configuration, bool isKubernetes, ILogger logger)
        {
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            var connectionString = configuration.GetConnectionString("IdentityConnectionString");

            if (isKubernetes && string.IsNullOrEmpty(dbPassword))
            {
                logger.LogError("DB_PASSWORD no está configurado en Kubernetes");
                throw new ArgumentNullException(nameof(dbPassword), "DB_PASSWORD no está configurado para Kubernetes");
            }

            return isKubernetes
                ? string.Format(connectionString, dbPassword)
                : connectionString;
        }

        private static void RegisterApplicationServices(
            IServiceCollection services,
            IConfiguration configuration,
            ILogger logger)
        {
            // 1. Configuración de UnitOfWork y repositorios
            services.AddScoped<IUnitOfWork>(provider =>
                new UnitOfWork(
                    provider.GetRequiredService<UserIdentityDbContext>(),
                    provider.GetRequiredService<ILogger<UnitOfWork>>()));

            services.AddScoped<IUserRepository>(provider =>
                new UserRepository(
                    provider.GetRequiredService<UserIdentityDbContext>(),
                    provider.GetRequiredService<IRepositoryResilience>().DbRetryPolicy));

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

            // 2. Configuración de HttpClient con políticas de resiliencia
            services.AddHttpClient<IExternalAuthService, ExternalAuthService>()
                .AddResiliencePolicies(provider =>
                    provider.GetRequiredService<IRepositoryResilience>());

            // 3. Configuración de Identity
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                ConfigureIdentityOptions(options);
            })
            .AddEntityFrameworkStores<UserIdentityDbContext>()
            .AddDefaultTokenProviders();

            // 4. Configuración JWT (usando configuración fuertemente tipada)
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // 5. Servicios de aplicación
            services.AddScopedServices();

            // 6. AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            // 7. Servicios gRPC
            services.AddGrpcServices(configuration);
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
        public static IHttpClientBuilder AddResiliencePolicies(
         this IHttpClientBuilder builder,
         Func<IServiceProvider, IRepositoryResilience> resilienceProvider)
        {
            return builder
                .AddPolicyHandler((services, request) => resilienceProvider(services).HttpRetryPolicy)
                .AddPolicyHandler((services, request) => resilienceProvider(services).HttpCircuitBreaker)
                .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));
        }

        public static IServiceCollection AddScopedServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IAuthService, AuthService>()
                .AddScoped<IHealthChecker, HealthChecker>()
                .AddScoped<IExternalAuthService, ExternalAuthService>();
        }

        private static IHttpClientBuilder AddTimeoutPolicy(
            this IHttpClientBuilder builder,
            TimeSpan timeout)
        {
            return builder.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(timeout));
        }
    }
}