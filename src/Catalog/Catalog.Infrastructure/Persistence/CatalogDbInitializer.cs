using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Extensions;

namespace Catalog.Infrastructure.Persistence
{
    public static class CatalogDbInitializer
    {
        public static async Task InitializeAsync(CatalogDbContext context, ILogger logger = null)
        {
            try
            {
                if (await context.Categories.AnyAsync())
                {
                    logger?.LogInformation("La base de datos ya contiene datos. No se ejecutará el seeding.");
                    return;
                }

                logger?.LogInformation("Iniciando seeding de datos iniciales para el catálogo...");

                // Usuario "sistema" para auditoría (puedes cambiarlo)
                const string systemUserId = "system-seed";

                // 1. Crear categorías
                var categories = new List<Category>
        {
            new Category
            {
                Id = Guid.NewGuid(),
                Name = "Electrónicos",
                Description = "Dispositivos electrónicos y gadgets tecnológicos"
            }.ApplyAudit(systemUserId, isNew: true),  // 👈 Aplica auditoría
            new Category
            {
                Id = Guid.NewGuid(),
                Name = "Ropa",
                Description = "Prendas de vestir para todas las edades"
            }.ApplyAudit(systemUserId, isNew: true),
            new Category
            {
                Id = Guid.NewGuid(),
                Name = "Hogar",
                Description = "Artículos para el hogar y decoración"
            }.ApplyAudit(systemUserId, isNew: true)
        };

                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();

                // 2. Crear productos
                var products = new List<Product>
        {
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Smartphone Premium",
                Description = "Último modelo con cámara de 108MP",
                Price = 899.99m,
                CategoryId = categories[0].Id,
                IsActive = true
            }.ApplyAudit(systemUserId, isNew: true),  // 👈 Auditoría aquí
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Laptop Ultradelgada",
                Description = "Laptop de alto rendimiento para profesionales",
                Price = 1299.99m,
                CategoryId = categories[0].Id,
                IsActive = true
            }.ApplyAudit(systemUserId, isNew: true),
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Camiseta Algodón",
                Description = "100% algodón orgánico, talla única",
                Price = 24.99m,
                CategoryId = categories[1].Id,
                IsActive = true
            }.ApplyAudit(systemUserId, isNew: true),
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Juego de Sábanas",
                Description = "Juego de sábanas de algodón egipcio",
                Price = 59.99m,
                CategoryId = categories[2].Id,
                IsActive = true
            }.ApplyAudit(systemUserId, isNew: true)
        };

                await context.Products.AddRangeAsync(products);
                await context.SaveChangesAsync();

                // 3. Añadir imágenes (opcional, también con auditoría si heredan de BaseAuditableEntity)
                var productImages = new List<ProductImage>
        {
            new ProductImage
            {
                Id = Guid.NewGuid(),
                ImageUrl = "https://example.com/images/smartphone-premium.jpg",
                ProductId = products[0].Id
            }.ApplyAudit(systemUserId, isNew: true),
            // ... más imágenes
        };

                await context.ProductImages.AddRangeAsync(productImages);
                await context.SaveChangesAsync();

                logger?.LogInformation("Seeding completado. {ProductCount} productos creados.", products.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error durante el seeding");
                throw;
            }
        }
    }
}