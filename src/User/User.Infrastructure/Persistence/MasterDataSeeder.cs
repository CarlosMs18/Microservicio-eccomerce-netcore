using Microsoft.AspNetCore.Identity;
using User.Application.Models;
using Serilog;

namespace User.Infrastructure.Persistence
{
    /// <summary>
    /// Seeder para datos maestros críticos en Production
    /// Solo roles esenciales, sin usuarios de prueba
    /// </summary>
    public static class MasterDataSeeder
    {
        public static async Task SeedAsync(
            UserIdentityDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            Log.Information("🌱 Iniciando seeding de datos maestros para Production");

            // 1. Crear solo roles esenciales para Production
            var essentialRoles = new List<ApplicationRole>
            {
                new ApplicationRole
                {
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new ApplicationRole
                {
                    Name = "Customer",
                    NormalizedName = "CUSTOMER"
                },
                new ApplicationRole
                {
                    Name = "Manager",
                    NormalizedName = "MANAGER"
                },
                new ApplicationRole
                {
                    Name = "User",
                    NormalizedName = "USER"
                }
            };

            foreach (var role in essentialRoles)
            {
                if (!await roleManager.RoleExistsAsync(role.Name))
                {
                    var result = await roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        Log.Information("✅ Rol creado: {RoleName}", role.Name);
                    }
                    else
                    {
                        Log.Error("❌ Error creando rol {RoleName}: {Errors}",
                            role.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    Log.Information("ℹ️ Rol ya existe: {RoleName}", role.Name);
                }
            }

            // 2. NO crear usuarios de prueba en Production
            // Los usuarios reales se registrarán vía API

            await context.SaveChangesAsync();
            Log.Information("✅ Seeding de datos maestros completado");
        }
    }
}