using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence
{
    public class CatalogDbContext: DbContext
    {
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }


        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var idProperty = entityType.FindProperty("Id");
                if (idProperty != null && idProperty.ClrType == typeof(Guid))
                {
                    idProperty.SetDefaultValueSql("NEWSEQUENTIALID()");
                }
            }

            modelBuilder.Entity<Product>()
               .HasMany(p => p.Images)
               .WithOne(pi => pi.Product)
               .HasForeignKey(pi => pi.ProductId)
               .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    
}
