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

                    // 1. Verificar si el producto existe (ahora usando Guid)
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

                    // 2. Verificar stock disponible (ahora usando Guid)
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

                    // 3. Si todo está bien, proceder con el agregado al carrito
                    _logger.LogInformation("✅ Producto {ProductId} validado correctamente. Stock disponible: {AvailableStock}",
                        productIdGuid, availableStock);

                    // Aquí iría la lógica para agregar al carrito
                    // Por ejemplo: await _cartRepository.AddProductToCartAsync(userId, productIdGuid, requestedQuantity);

                    return new AddToCartResponse
                    {
                        Success = true,
                        Message = "Producto agregado al carrito exitosamente",
                        ProductId = productIdGuid,
                        AvailableStock = availableStock,
                        RequestedQuantity = requestedQuantity
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