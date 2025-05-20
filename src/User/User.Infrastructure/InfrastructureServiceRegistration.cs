using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Interfaces;
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
            // 1. Configuración de la DB con manejo dinámico de contraseña
            services.AddDbContext<UserIdentityDbContext>(options =>
            {
                var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
                var connectionString = configuration.GetConnectionString("IdentityConnectionString");

                if (string.IsNullOrEmpty(dbPassword))
                    throw new ArgumentNullException("DB_PASSWORD no está configurado");

                // Reemplaza {0} por la contraseña (sanitizada para logs)
                var formattedConnectionString = string.Format(connectionString, dbPassword);

                options.UseSqlServer(formattedConnectionString,
                    b => b.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName));
            });

            // 2. Configuración STRICTA de Identity
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // 🔐 Reglas de contraseña (ajusta según requisitos)
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;

                // 📧 Reglas de usuario
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

                // ⏱️ Bloqueo por intentos fallidos
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<UserIdentityDbContext>()
            .AddDefaultTokenProviders();

            // 3. Configuración del JWT (con fallback a configuración local)
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? configuration["JwtSettings:Key"];
            services.Configure<JwtSettings>(options =>
            {
                options.Key = jwtKey;
                options.Issuer = configuration["JwtSettings:Issuer"];
                options.Audience = configuration["JwtSettings:Audience"];
                options.DurationInMinutes = configuration.GetValue<int>("JwtSettings:DurationInMinutes");
                options.HoursForRefreshToken = configuration.GetValue<int>("JwtSettings:HoursForRefreshToken");
            });

            // 4. Servicios personalizados
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IHealthChecker, HealthChecker>();
            services.AddScoped<IExternalAuthService, ExternalAuthService>();
            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

            // 5. AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            // 6. gRPC Services (configuración dinámica)
            services.AddGrpcServices(configuration);

            return services;
        }
    }
}