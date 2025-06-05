using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence
{
    public class CatalogDbContext : DbContext
    {
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!;

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración automática de GUIDs secuenciales para mejor rendimiento
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var idProperty = entityType.FindProperty("Id");
                if (idProperty != null && idProperty.ClrType == typeof(Guid))
                {
                    idProperty.SetDefaultValueSql("NEWSEQUENTIALID()");
                }
            }

            // Configuración de relaciones Category -> Products
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Evita borrado accidental de categorías con productos

            // Configuración de relaciones Product -> Images
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Images)
                .WithOne(pi => pi.Product)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Borra imágenes cuando se borra el producto

            // Índices para optimización de consultas
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique(); // Nombres de categorías únicos

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.CategoryId, p.IsActive }); // Para consultas por categoría y estado

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Name); // Para búsquedas por nombre de producto

            // Configuraciones adicionales de propiedades
            ConfigureEntityProperties(modelBuilder);
        }

        private void ConfigureEntityProperties(ModelBuilder modelBuilder)
        {
            // Configuración de Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);
            });

            // Configuración de Product
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.IsActive)
                    .HasDefaultValue(true);

                entity.Property(p => p.Stock)
                    .HasDefaultValue(0);
            });

            // Configuración de ProductImage
            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.Property(e => e.ImageUrl)
                    .IsRequired()
                    .HasMaxLength(500);
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Aquí podrías agregar lógica de auditoría automática si lo necesitas
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}