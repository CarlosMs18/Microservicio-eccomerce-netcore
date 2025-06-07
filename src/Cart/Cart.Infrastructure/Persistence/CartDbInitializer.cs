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

                // 1. Crear items de carrito de prueba primero
                var cartItems1 = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
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
                        ProductId = Guid.NewGuid(),
                        ProductName = "Laptop Ultradelgada",
                        ProductDescription = "Laptop profesional con procesador Intel i7 y 16GB RAM",
                        ProductImageUrl = "https://example.com/images/laptop.jpg",
                        CategoryId = Guid.NewGuid(),
                        CategoryName = "Computadoras",
                        Price = 1299.99m,
                        Quantity = 2
                    }.ApplyAudit(systemUserId, isNew: true)
                };

                var cartItems2 = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
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

                // 2. Crear carritos de prueba con TotalAmount calculado
                var carts = new List<Domain.Cart>
                {
                    new Domain.Cart
                    {
                        Id = Guid.NewGuid(),
                        Items = cartItems1,
                        TotalAmount = cartItems1.Sum(item => item.Price * item.Quantity) // 899.99 + (1299.99 * 2) = 3499.97
                    }.ApplyAudit(systemUserId, isNew: true),

                    new Domain.Cart
                    {
                        Id = Guid.NewGuid(),
                        Items = cartItems2,
                        TotalAmount = cartItems2.Sum(item => item.Price * item.Quantity) // 24.99 * 3 = 74.97
                    }.ApplyAudit(systemUserId, isNew: true)
                };

                // 3. Asignar CartId a los items
                foreach (var item in cartItems1)
                {
                    item.CartId = carts[0].Id;
                }

                foreach (var item in cartItems2)
                {
                    item.CartId = carts[1].Id;
                }

                // 4. Guardar en base de datos
                await context.Carts.AddRangeAsync(carts);
                await context.SaveChangesAsync();

                var allCartItems = cartItems1.Concat(cartItems2).ToList();
                await context.CartItems.AddRangeAsync(allCartItems);
                await context.SaveChangesAsync();

                logger?.LogInformation("Seeding completado. {CartCount} carritos y {ItemCount} items creados.",
                    carts.Count, allCartItems.Count);

                logger?.LogInformation("Carritos creados:");
                logger?.LogInformation("  - Carrito 1: {ItemCount} items, Total: ${TotalAmount:F2}",
                    cartItems1.Count, carts[0].TotalAmount);
                logger?.LogInformation("  - Carrito 2: {ItemCount} items, Total: ${TotalAmount:F2}",
                    cartItems2.Count, carts[1].TotalAmount);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error durante el seeding del carrito");
                throw;
            }
        }
    }
}