using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Catalog.Infrastructure.SyncDataServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Interfaces;
using System;

namespace Catalog.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<CatalogDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("CatalogConnection"), // Nombre claro
                    sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(120);
                        sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName); // Para migraciones
                    }
                );
             
                // Configuración adicional para GUIDs y otros
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            // Configuración específica para GUIDs como clave primaria
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IAsyncRepository<>), typeof(RepositoryBase<>));
            //services.AddScoped<IExternalAuthService, UserHttpService>();

            return services;
        }
    }
}