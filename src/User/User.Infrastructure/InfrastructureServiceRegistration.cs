using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Core.Interfaces;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Resilience;
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
            // =============================================
            // 1. Configuración inicial y logging
            // =============================================
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            var logger = loggerFactory.CreateLogger("InfrastructureServiceRegistration");
            var isKubernetes = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

            // =============================================
            // 2. Configuración de la base de datos
            // =============================================
            services.AddDbContext<UserIdentityDbContext>(options =>
            {
                var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
                var connectionString = configuration.GetConnectionString("IdentityConnectionString");

                if (isKubernetes && string.IsNullOrEmpty(dbPassword))
                {
                    logger.LogError("DB_PASSWORD no está configurado en Kubernetes");
                    throw new ArgumentNullException(nameof(dbPassword), "DB_PASSWORD no está configurado para Kubernetes");
                }

                var formattedConnectionString = isKubernetes
                    ? string.Format(connectionString, dbPassword)
                    : connectionString;

                options.UseSqlServer(formattedConnectionString,
                    b => b.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName));
            });

            // =============================================
            // 3. Configuración de políticas de resiliencia
            // =============================================
            services.AddResiliencePolicies();

            // =============================================
            // 4. Configuración de UnitOfWork y repositorios
            // =============================================
            services.AddScoped<IUnitOfWork>(provider =>
            {
                var context = provider.GetRequiredService<UserIdentityDbContext>();
                var logger = provider.GetRequiredService<ILogger<UnitOfWork>>();
                return new UnitOfWork(context, logger);
            });

            services.AddScoped<IUserRepository>(provider =>
            {
                var context = provider.GetRequiredService<UserIdentityDbContext>();
                var resilience = provider.GetRequiredService<IRepositoryResilience>();
                return new UserRepository(context, resilience.DbRetryPolicy);
            });

            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

            // =============================================
            // 5. Configuración de HttpClient con Polly
            // =============================================
            services.AddHttpClient<IExternalAuthService, ExternalAuthService>()
                .AddPolicyHandler((provider, request) =>
                    provider.GetRequiredService<IRepositoryResilience>().HttpRetryPolicy)
                .AddPolicyHandler((provider, request) =>
                    provider.GetRequiredService<IRepositoryResilience>().HttpCircuitBreaker);

            // =============================================
            // 6. Configuración de Identity Core
            // =============================================
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Configuración de contraseñas
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;

                // Configuración de usuarios
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

                // Configuración de bloqueo
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<UserIdentityDbContext>()
            .AddDefaultTokenProviders();

            // =============================================
            // 7. Configuración de JWT
            // =============================================
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? configuration["JwtSettings:Key"];
            
            services.Configure<JwtSettings>(options =>
            {
                options.Key = jwtKey;
                options.Issuer = configuration["JwtSettings:Issuer"];
                options.Audience = configuration["JwtSettings:Audience"];
                options.DurationInMinutes = configuration.GetValue<int>("JwtSettings:DurationInMinutes");
                options.HoursForRefreshToken = configuration.GetValue<int>("JwtSettings:HoursForRefreshToken");
            });

            // =============================================
            // 8. Registro de servicios de aplicación
            // =============================================
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IHealthChecker, HealthChecker>();
            services.AddScoped<IExternalAuthService, ExternalAuthService>();

            // =============================================
            // 9. Configuración de AutoMapper
            // =============================================
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            // =============================================
            // 10. Configuración de servicios gRPC
            // =============================================
            services.AddGrpcServices(configuration);

            logger.LogInformation("Servicios de infraestructura configurados correctamente");
            return services;
        }
    }
}