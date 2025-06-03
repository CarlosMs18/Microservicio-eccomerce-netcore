using Cart.Application.Contracts.External;
using Cart.Application.DTos.External;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Core.Handlers;

namespace Cart.Application.Features.Carts.Commands
{
    public class AddProductToCartCommand : IRequest<AddToCartResponse>
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }

        public class AddProductToCartCommandHandler : BaseHandler, IRequestHandler<AddProductToCartCommand, AddToCartResponse>
        {
            private readonly ICatalogService _catalogService;
            private readonly ILogger<AddProductToCartCommandHandler> _logger;

            public AddProductToCartCommandHandler(
                IHttpContextAccessor httpContextAccessor,
                ICatalogService catalogService,
                ILogger<AddProductToCartCommandHandler> logger
                ) : base(httpContextAccessor)
            {
                _catalogService = catalogService;
                _logger = logger;
            }

            public async Task<AddToCartResponse> Handle(AddProductToCartCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var productIdString = request.ProductId;
                    var requestedQuantity = request.Quantity;

                    _logger.LogInformation("🛒 Iniciando proceso para agregar producto {ProductId} con cantidad {Quantity}",
                        productIdString, requestedQuantity);

                    // Validar que el ProductId sea un GUID válido
                    if (!Guid.TryParse(productIdString, out var productIdGuid))
                    {
                        _logger.LogWarning("❌ ProductId {ProductId} no es un GUID válido", productIdString);
                        return new AddToCartResponse
                        {
                            Success = false,
                            Message = "El identificador del producto no es válido",
                            ProductId = Guid.Empty,
                            AvailableStock = 0,
                            RequestedQuantity = requestedQuantity
                        };
                    }

                    // 1. Verificar si el producto existe (responsabilidad única)
                    var productExists = await _catalogService.ProductExistsAsync(productIdGuid);

                    if (!productExists)
                    {
                        _logger.LogWarning("❌ Producto {ProductId} no existe", productIdGuid);
                        return new AddToCartResponse
                        {
                            Success = false,
                            Message = $"El producto {productIdGuid} no existe o no está disponible",
                            ProductId = productIdGuid,
                            AvailableStock = 0,
                            RequestedQuantity = requestedQuantity
                        };
                    }

                    // 2. Verificar stock disponible (responsabilidad única)
                    var availableStock = await _catalogService.GetProductStockAsync(productIdGuid);

                    if (availableStock < requestedQuantity)
                    {
                        _logger.LogWarning("📦 Stock insuficiente para producto {ProductId}. Stock disponible: {AvailableStock}, Solicitado: {RequestedQuantity}",
                            productIdGuid, availableStock, requestedQuantity);

                        return new AddToCartResponse
                        {
                            Success = false,
                            Message = $"Stock insuficiente. Disponible: {availableStock}, Solicitado: {requestedQuantity}",
                            ProductId = productIdGuid,
                            AvailableStock = availableStock,
                            RequestedQuantity = requestedQuantity
                        };
                    }

                    // 3. Obtener detalles del producto para el carrito (responsabilidad única)
                    var productDetails = await _catalogService.GetProductDetailsAsync(productIdGuid);

                    if (productDetails == null)
                    {
                        _logger.LogError("❌ Error: No se pudieron obtener los detalles del producto {ProductId}", productIdGuid);
                        return new AddToCartResponse
                        {
                            Success = false,
                            Message = "Error al obtener los detalles del producto",
                            ProductId = productIdGuid,
                            AvailableStock = availableStock,
                            RequestedQuantity = requestedQuantity
                        };
                    }

                    // 4. Crear CartItem con todos los detalles del producto
                    _logger.LogInformation("✅ Producto {ProductId} validado correctamente. Creando item del carrito...", productIdGuid);

                    var cartItem = new Domain.CartItem
                    {
                        ProductId = productDetails.Id,
                        ProductName = productDetails.Name,
                        ProductDescription = productDetails.Description,
                        Price = productDetails.Price,
                        Quantity = requestedQuantity,
                        // Tomar la primera imagen si existe
                        ProductImageUrl = productDetails.Images?.FirstOrDefault()?.ImageUrl,
                        CategoryId = productDetails.Category.Id,
                        CategoryName = productDetails.Category.Name
                    };

                    // Aquí iría la lógica para agregar al carrito
                    // Por ejemplo: await _cartRepository.AddItemToCartAsync(userId, cartItem);

                    _logger.LogInformation("🎉 CartItem creado: {ProductName} - Cantidad: {Quantity} - Subtotal: {Subtotal}",
                        cartItem.ProductName, cartItem.Quantity, cartItem.Subtotal);

                    return new AddToCartResponse
                    {
                        Success = true,
                        Message = "Producto agregado al carrito exitosamente",
                        ProductId = productIdGuid,
                        AvailableStock = availableStock, // Usamos el stock que ya obtuvimos
                        RequestedQuantity = requestedQuantity,
                        // Información adicional del producto agregado
                        ProductName = productDetails.Name,
                        ProductImageUrl = cartItem.ProductImageUrl,
                        Subtotal = cartItem.Subtotal
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error inesperado al agregar producto {ProductId} al carrito",
                        request.ProductId);

                    return new AddToCartResponse
                    {
                        Success = false,
                        Message = "Error interno del servidor al procesar la solicitud",
                        ProductId = Guid.TryParse(request.ProductId, out var errorGuid) ? errorGuid : Guid.Empty,
                        AvailableStock = 0,
                        RequestedQuantity = request.Quantity
                    };
                }
            }
        }
    }
}