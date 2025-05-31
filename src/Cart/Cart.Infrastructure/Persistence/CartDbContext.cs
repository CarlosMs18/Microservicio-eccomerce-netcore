using Cart.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cart.Infrastructure.Persistence
{
    public class CartDbContext :DbContext
    {
        public DbSet<Cart.Domain.Cart> Carts { get; set; }  
        public DbSet<CartItem> CartItems { get; set; }

        public CartDbContext(DbContextOptions<CartDbContext> options) : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Cart
            modelBuilder.Entity<Cart.Domain.Cart>(entity =>
            {
                // Clave primaria con GUID autogenerado
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id)
                    .HasDefaultValueSql("NEWSEQUENTIALID()") // SQL Server
                    .ValueGeneratedOnAdd();

                // Configuración de auditoría
                entity.Property(c => c.CreatedBy)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.CreatedDate)
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(c => c.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.UpdatedDate);

                // ÍNDICES PARA PERFORMANCE
                // Necesitamos agregar UserId como propiedad en Cart o crear índice en CreatedBy
                entity.HasIndex(c => c.CreatedBy)
                    .HasDatabaseName("IX_Carts_CreatedBy");

                // Índice para búsquedas por fecha de creación
                entity.HasIndex(c => c.CreatedDate)
                    .HasDatabaseName("IX_Carts_CreatedDate");

                // Índice para limpiar carritos expirados por UpdatedDate
                entity.HasIndex(c => c.UpdatedDate)
                    .HasDatabaseName("IX_Carts_UpdatedDate");

                // Relación uno a muchos con CartItems
                entity.HasMany(c => c.Items)
                    .WithOne()
                    .HasForeignKey(ci => ci.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Nombre de tabla
                entity.ToTable("Carts");
            });

            // Configuración de CartItem
            modelBuilder.Entity<CartItem>(entity =>
            {
                // Clave primaria con GUID autogenerado
                entity.HasKey(ci => ci.Id);
                entity.Property(ci => ci.Id)
                    .HasDefaultValueSql("NEWSEQUENTIALID()") // SQL Server
                    .ValueGeneratedOnAdd();

                // Propiedades específicas de CartItem
                entity.Property(ci => ci.CartId)
                    .IsRequired();

                entity.Property(ci => ci.ProductId)
                    .IsRequired();

                entity.Property(ci => ci.ProductName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(ci => ci.Price)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)");

                entity.Property(ci => ci.Quantity)
                    .IsRequired()
                    .HasDefaultValue(1);

                // Configuración de auditoría
                entity.Property(ci => ci.CreatedBy)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(ci => ci.CreatedDate)
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(ci => ci.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(ci => ci.UpdatedDate);

                // ÍNDICES PARA PERFORMANCE
                // Índice en CartId (FK)
                entity.HasIndex(ci => ci.CartId)
                    .HasDatabaseName("IX_CartItems_CartId");

                // Índice en ProductId para búsquedas rápidas
                entity.HasIndex(ci => ci.ProductId)
                    .HasDatabaseName("IX_CartItems_ProductId");

                // Índice compuesto único: Un producto por carrito (evita duplicados)
                entity.HasIndex(ci => new { ci.CartId, ci.ProductId })
                    .IsUnique()
                    .HasDatabaseName("IX_CartItems_CartId_ProductId");

                // Índice para consultas por fecha de creación
                entity.HasIndex(ci => ci.CreatedDate)
                    .HasDatabaseName("IX_CartItems_CreatedDate");

                // Índice compuesto para búsquedas frecuentes
                entity.HasIndex(ci => new { ci.CartId, ci.CreatedDate })
                    .HasDatabaseName("IX_CartItems_CartId_CreatedDate");

                // Nombre de tabla
                entity.ToTable("CartItems");
            });
        }


        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {

            return base.SaveChangesAsync(cancellationToken);
        }
    }

   
}
