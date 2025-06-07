using Cart.Application.Contracts.Persistence;
using Cart.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Events;
using Shared.Core.Extensions;
using System.Text.Json;

namespace Cart.Infrastructure.Services.Messaging
{
    public class ProductPriceChangedConsumer
    {
        private readonly ILogger<ProductPriceChangedConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ProductPriceChangedConsumer(
            ILogger<ProductPriceChangedConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task HandleAsync(string message)
        {
            try
            {
                _logger.LogInformation($"Procesando evento de cambio de precio: {message}");

                // Deserializar el evento
                var productPriceChangedEvent = JsonSerializer.Deserialize<ProductPriceChangedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (productPriceChangedEvent == null)
                {
                    _logger.LogWarning("No se pudo deserializar el evento ProductPriceChangedEvent");
                    return;
                }

                // Validar datos del evento
                if (productPriceChangedEvent.ProductId == Guid.Empty)
                {
                    _logger.LogWarning("ProductId inválido en el evento");
                    return;
                }

                if (productPriceChangedEvent.NewPrice <= 0)
                {
                    _logger.LogWarning($"Precio inválido en el evento: {productPriceChangedEvent.NewPrice}");
                    return;
                }

                // Usar scope para obtener dependencias
                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Actualizar precio en todos los carritos que contengan este producto
                await UpdateProductPriceInCarts(unitOfWork, productPriceChangedEvent);

                _logger.LogInformation($"Precio actualizado exitosamente en carritos para producto {productPriceChangedEvent.ProductId}: {productPriceChangedEvent.OldPrice} -> {productPriceChangedEvent.NewPrice}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error procesando evento de cambio de precio: {message}");
                throw; // Re-lanzar para que RabbitMQ maneje el retry/reject
            }
        }

        private async Task UpdateProductPriceInCarts(IUnitOfWork unitOfWork, ProductPriceChangedEvent eventData)
        {
            try
            {
                // Obtener todos los items de carrito que contengan este producto
                var cartItems = await unitOfWork.CartItemRepository.GetByProductIdAsync(eventData.ProductId);

                if (!cartItems.Any())
                {
                    _logger.LogInformation($"No se encontraron items en carritos para el producto {eventData.ProductId}");
                    return;
                }

                _logger.LogInformation($"Actualizando precio en {cartItems.Count} items de carrito para producto {eventData.ProductId}");

                var affectedCartIds = new HashSet<Guid>();

                // Actualizar el precio en cada item
                foreach (var cartItem in cartItems)
                {
                    var oldItemPrice = cartItem.Price;
                    cartItem.Price = eventData.NewPrice;

                    // Aplicar auditoría usando tu extensión
                    cartItem.ApplyAudit("SYSTEM", isNew: false);

                    // Agregar cartId a la lista de carritos afectados
                    affectedCartIds.Add(cartItem.CartId);

                    _logger.LogDebug($"Item actualizado - CartId: {cartItem.CartId}, ProductId: {cartItem.ProductId}, Precio: {oldItemPrice} -> {cartItem.Price}, Subtotal: {cartItem.Subtotal}");
                }

                // Guardar cambios de los items
                await unitOfWork.Complete();

                // Actualizar totales de los carritos afectados
                await UpdateCartTotals(unitOfWork, affectedCartIds);

                _logger.LogInformation($"Actualización completada para {cartItems.Count} items en {affectedCartIds.Count} carritos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error actualizando precios en carritos para producto {eventData.ProductId}");
                throw;
            }
        }

        private async Task UpdateCartTotals(IUnitOfWork unitOfWork, HashSet<Guid> affectedCartIds)
        {
            try
            {
                foreach (var cartId in affectedCartIds)
                {
                    // Obtener el carrito
                    var cart = await unitOfWork.CartRepository.GetByIdAsync(cartId);
                    if (cart == null)
                    {
                        _logger.LogWarning($"Carrito {cartId} no encontrado para actualizar totales");
                        continue;
                    }

                    // Obtener todos los items del carrito para recalcular total
                    var cartItems = await unitOfWork.CartItemRepository.GetItemsByCartIdAsync(cartId);

                    // Recalcular total del carrito usando la propiedad calculada Subtotal
                    var newTotal = cartItems.Sum(item => item.Subtotal);

                    // Solo actualizar si el total cambió
                    if (cart.TotalAmount != newTotal)
                    {
                        var oldTotal = cart.TotalAmount;
                        cart.TotalAmount = newTotal;

                        // Aplicar auditoría
                        cart.ApplyAudit("SYSTEM", isNew: false);

                        _logger.LogDebug($"Total actualizado para carrito {cartId}: {oldTotal} -> {cart.TotalAmount}");
                    }
                }

                await unitOfWork.Complete();
                _logger.LogInformation($"Totales actualizados para {affectedCartIds.Count} carritos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando totales de carritos");
                throw;
            }
        }
    }
}