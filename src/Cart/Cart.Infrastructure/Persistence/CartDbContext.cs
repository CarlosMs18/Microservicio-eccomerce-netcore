﻿using Cart.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cart.Infrastructure.Persistence
{
    public class CartDbContext : DbContext
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

                entity.Property(c => c.TotalAmount)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0.00m);
                entity.Property(c => c.CreatedDate)
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(c => c.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.UpdatedDate);

                // ÍNDICES PARA PERFORMANCE
                entity.HasIndex(c => c.CreatedBy)
                    .HasDatabaseName("IX_Carts_CreatedBy");

                entity.HasIndex(c => c.CreatedDate)
                    .HasDatabaseName("IX_Carts_CreatedDate");

                entity.HasIndex(c => c.UpdatedDate)
                    .HasDatabaseName("IX_Carts_UpdatedDate");

                // Relación uno a muchos con CartItems
                entity.HasMany(c => c.Items)
                    .WithOne(ci => ci.Cart)
                    .HasForeignKey(ci => ci.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => c.TotalAmount)
                    .HasDatabaseName("IX_Carts_TotalAmount");
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

                // NUEVOS CAMPOS AGREGADOS
                entity.Property(ci => ci.ProductDescription)
                    .IsRequired()
                    .HasMaxLength(1000); // Ajusta el tamaño según necesites

                entity.Property(ci => ci.ProductImageUrl)
                    .HasMaxLength(500); // URL puede ser nula

                entity.Property(ci => ci.CategoryId)
                    .IsRequired();

                entity.Property(ci => ci.CategoryName)
                    .IsRequired()
                    .HasMaxLength(100);

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
                entity.HasIndex(ci => ci.CartId)
                    .HasDatabaseName("IX_CartItems_CartId");

                entity.HasIndex(ci => ci.ProductId)
                    .HasDatabaseName("IX_CartItems_ProductId");

                // Nuevo índice para CategoryId
                entity.HasIndex(ci => ci.CategoryId)
                    .HasDatabaseName("IX_CartItems_CategoryId");

                entity.HasIndex(ci => new { ci.CartId, ci.ProductId })
                    .IsUnique()
                    .HasDatabaseName("IX_CartItems_CartId_ProductId");

                entity.HasIndex(ci => ci.CreatedDate)
                    .HasDatabaseName("IX_CartItems_CreatedDate");

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