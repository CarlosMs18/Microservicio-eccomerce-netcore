using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Net;
using User.Application.Models;

namespace User.Infrastructure.Persistence
{
    public class UserIdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string,
    IdentityUserClaim<string>, ApplicationUserRole, IdentityUserLogin<string>,
    IdentityRoleClaim<string>, IdentityUserToken<string>>
    {
        public UserIdentityDbContext(DbContextOptions<UserIdentityDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            builder.Entity<ApplicationRole>()
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            builder.Entity<ApplicationUserRole>(b =>
            {
                b.HasKey(ur => new { ur.UserId, ur.RoleId });
      
            });

            // Configuración de índices para mejor performance
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.NormalizedEmail)
                .IsUnique();
        }
    }
}
