using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using User.Infrastructure.Persistence;
using User.Application.Models;


namespace User.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context
            services.RemoveAll(typeof(DbContextOptions<UserIdentityDbContext>));
            services.RemoveAll(typeof(UserIdentityDbContext));

            // Add in-memory database
            services.AddDbContext<UserIdentityDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Configure Identity for testing
            services.Configure<IdentityOptions>(options =>
            {
                // Relaxed password requirements for testing
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;

                // User settings
                options.User.RequireUniqueEmail = true;
            });

            // Build service provider and ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();

            // Ensure database is created and clean
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            // Seed roles if needed
            SeedRoles(scope.ServiceProvider).Wait();
        });

        builder.UseEnvironment("Testing");
    }

    private static async Task SeedRoles(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roles = new[] { "User", "Admin" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    public async Task<UserIdentityDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UserIdentityDbContext>();

        // Clear all users but keep roles
        var users = context.Users.ToList();
        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}