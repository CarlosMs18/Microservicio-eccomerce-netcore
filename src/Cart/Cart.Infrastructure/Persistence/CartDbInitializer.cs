using Cart.Domain;
using Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Core.Extensions;

namespace Cart.Infrastructure.Persistence
{
    public static class CartDbInitializer
    {
        public static async Task InitializeAsync(CartDbContext context, ILogger logger = null)
        {
            try
            {
                if (await context.Carts.AnyAsync())
                {
                    logger?.LogInformation("La base de datos ya contiene datos. No se ejecutará el seeding.");
                    return;
                }

                logger?.LogInformation("Iniciando seeding de datos iniciales para el carrito...");

                // Usuario "sistema" para auditoría
                const string systemUserId = "system-seed";

                // 1. Crear carritos de prueba
                var carts = new List<Domain.Cart>
                {
                    new Domain.Cart
                    {
                        Id = Guid.NewGuid(),
                        Items = new List<CartItem>()
                    }.ApplyAudit(systemUserId, isNew: true),
                    new Domain.Cart
                    {
                        Id = Guid.NewGuid(),
                        Items = new List<CartItem>()
                    }.ApplyAudit(systemUserId, isNew: true)
                };

                await context.Carts.AddRangeAsync(carts);
                await context.SaveChangesAsync();

                // 2. Crear items de carrito de prueba con los nuevos campos
                var cartItems = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = carts[0].Id,
                        ProductId = Guid.NewGuid(),
                        ProductName = "Smartphone Premium",
                        ProductDescription = "Smartphone de última generación con 256GB de almacenamiento",
                        ProductImageUrl = "https://example.com/images/smartphone.jpg",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Electrónicos",
                        Price = 899.99m,
                        Quantity = 1
                    }.ApplyAudit(systemUserId, isNew: true),

                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = carts[0].Id,
                        ProductId = Guid.NewGuid(),
                        ProductName = "Laptop Ultradelgada",
                        ProductDescription = "Laptop profesional con procesador Intel i7 y 16GB RAM",
                        ProductImageUrl = "https://example.com/images/laptop.jpg",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Computadoras",
                        Price = 1299.99m,
                        Quantity = 2
                    }.ApplyAudit(systemUserId, isNew: true),

                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = carts[1].Id,
                        ProductId = Guid.NewGuid(),
                        ProductName = "Camiseta Algodón",
                        ProductDescription = "Camiseta 100% algodón, talla M, color azul",
                        ProductImageUrl = "https://example.com/images/camiseta.jpg",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Ropa",
                        Price = 24.99m,
                        Quantity = 3
                    }.ApplyAudit(systemUserId, isNew: true)
                };

                await context.CartItems.AddRangeAsync(cartItems);
                await context.SaveChangesAsync();

                logger?.LogInformation("Seeding completado. {CartCount} carritos y {ItemCount} items creados.",
                    carts.Count, cartItems.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error durante el seeding del carrito");
                throw;
            }
        }
    }
}