using Microsoft.AspNetCore.Identity;
using User.Application.Models;

namespace User.Infrastructure.Persistence
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            UserIdentityDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            // 1. Crear roles base
            var roles = new List<ApplicationRole>
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

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role.Name))
                {
                    await roleManager.CreateAsync(role);
                }
            }

            // 2. Crear usuario admin
            if (await userManager.FindByEmailAsync("admin@example.com") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@example.com",
                    Email = "admin@example.com",
                    FirstName = "Super",
                    LastName = "Admin",
                    EmailConfirmed = true,
                 
                };

                await userManager.CreateAsync(admin, "AdminPass123!");
                await userManager.AddToRoleAsync(admin, "Admin");
                await userManager.AddToRoleAsync(admin, "Manager"); // Rol adicional
            }

            // 3. Crear usuario cliente de prueba
            if (await userManager.FindByEmailAsync("customer@example.com") == null)
            {
                var customer = new ApplicationUser
                {
                    UserName = "customer@example.com",
                    Email = "customer@example.com",
                    FirstName = "John",
                    LastName = "Doe",
                    EmailConfirmed = true,
                   

                };

                await userManager.CreateAsync(customer, "CustomerPass123!");
                await userManager.AddToRoleAsync(customer, "Customer");
            }

            // 4. Guardar cambios adicionales si es necesario
            await context.SaveChangesAsync();
        }
    }
}