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
using User.Infrastructure.Services;

namespace User.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Configuración de la DB
            services.AddDbContext<UserIdentityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("IdentityConnectionString"),
                b => b.MigrationsAssembly(typeof(UserIdentityDbContext).Assembly.FullName)));

            // 2. Configuración STRICTA de Identity (contraseñas, usuarios, etc.)
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // 🔐 Reglas de contraseña (customiza según tus necesidades)
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;

                // 📧 Reglas de usuario
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

                // ⏱️ Bloqueo por intentos fallidos (opcional)
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<UserIdentityDbContext>()
            .AddDefaultTokenProviders(); // Para recuperación de contraseñas

            // 3. Configuración del JWT (solo generación, NO validación)
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // 4. Servicios personalizados
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IExternalAuthService,  ExternalAuthService>();   
            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));

            // 5. AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}