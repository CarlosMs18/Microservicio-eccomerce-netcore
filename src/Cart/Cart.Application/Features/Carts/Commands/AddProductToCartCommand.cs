using Cart.Application.Contracts.External;
using Cart.Application.Contracts.Persistence;
using Cart.Application.DTos.External;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Core.Extensions;
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
            private readonly IUnitOfWork _unitOfWork;
            private readonly ILogger<AddProductToCartCommandHandler> _logger;

            public AddProductToCartCommandHandler(
                IHttpContextAccessor httpContextAccessor,
                ICatalogService catalogService,
                IUnitOfWork unitOfWork,
                ILogger<AddProductToCartCommandHandler> logger
                ) : base(httpContextAccessor)
            {
                _catalogService = catalogService;
                _unitOfWork = unitOfWork;
                _logger = logger;
            }

            public async Task<AddToCartResponse> Handle(AddProductToCartCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var productIdString = request.ProductId;
                    var requestedQuantity = request.Quantity;
                    var currentUserId = UserId; // Usando la propiedad de BaseHandler

                    // Validación de usuario autenticado
                    if (string.IsNullOrEmpty(currentUserId))
                    {
                        _logger.LogWarning("❌ Usuario no autenticado intentando agregar producto al carrito");
                        return new AddToCartResponse
                        {
                            Success = false,
                            Message = "Usuario no autenticado",
                            ProductId = Guid.Empty,
                            AvailableStock = 0,
                            RequestedQuantity = requestedQuantity
                        };
                    }

                    _logger.LogInformation("🛒 Usuario {UserId} iniciando proceso para agregar producto {ProductId} con cantidad {Quantity}",
                        currentUserId, productIdString, requestedQuantity);

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

                    // 1. Verificar si el producto existe
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

                    // 2. Verificar stock disponible
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

                    // 3. Obtener detalles del producto
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

                    // ==================== PERSISTENCIA CON TRANSACCIÓN AUTOMÁTICA ====================
                    // 🔥 EF Core maneja la transacción automáticamente - Compatible con Retry Policy

                    // 4. Obtener o crear el carrito del usuario
                    // ✅ CORREGIDO: Sin AsNoTracking para permitir que EF rastree los cambios
                    var existingCart = await _unitOfWork.CartRepository.GetCartByUserIdAsync(currentUserId);

                    Domain.Cart cart;
                    bool isNewCart = false;

                    if (existingCart == null)
                    {
                        // Crear nuevo carrito con auditoría
                        cart = new Domain.Cart
                        {
                            Id = Guid.NewGuid(),
                            Items = new List<Domain.CartItem>()
                        }.ApplyAudit(currentUserId, isNew: true);

                        _unitOfWork.CartRepository.Add(cart);
                        isNewCart = true;
                        _logger.LogInformation("🛒 Nuevo carrito creado para usuario {UserId}", currentUserId);
                    }
                    else
                    {
                        // ✅ CORREGIDO: Usar el carrito existente que ya está siendo rastreado por EF
                        cart = existingCart;
                        cart.ApplyAudit(currentUserId, isNew: false); // Actualizar auditoría
                        _logger.LogInformation("🛒 Carrito existente encontrado para usuario {UserId}", currentUserId);
                    }

                    // 5. Verificar si el producto ya existe en el carrito
                    var existingCartItem = await _unitOfWork.CartItemRepository
                        .GetByCartAndProductAsync(cart.Id, productIdGuid);

                    if (existingCartItem != null)
                    {
                        // Actualizar cantidad del item existente con auditoría
                        existingCartItem.Quantity += requestedQuantity;
                        existingCartItem.ApplyAudit(currentUserId, isNew: false);
                        _unitOfWork.CartItemRepository.Update(existingCartItem);

                        _logger.LogInformation("📦 Cantidad actualizada para producto {ProductId} en carrito. Nueva cantidad: {Quantity}",
                            productIdGuid, existingCartItem.Quantity);
                    }
                    else
                    {
                        // 6. Crear nuevo CartItem con auditoría
                        var cartItem = new Domain.CartItem
                        {
                            Id = Guid.NewGuid(),
                            CartId = cart.Id,
                            ProductId = productIdGuid,
                            ProductName = productDetails.Name,
                            ProductDescription = productDetails.Description,
                            Price = productDetails.Price,
                            Quantity = requestedQuantity,
                            ProductImageUrl = productDetails.Images?.FirstOrDefault()?.ImageUrl,
                            CategoryId = productDetails.Category.Id,
                            CategoryName = productDetails.Category.Name,
                            Cart = cart
                        }.ApplyAudit(currentUserId, isNew: true);

                        _unitOfWork.CartItemRepository.Add(cartItem);
                        _logger.LogInformation("🎉 Nuevo CartItem creado: {ProductName} - Cantidad: {Quantity} - Subtotal: {Subtotal}",
                            cartItem.ProductName, cartItem.Quantity, cartItem.Subtotal);
                    }

                    // ✅ CORREGIDO: Solo actualizar el carrito si no es nuevo (ya se agregó arriba)
                    if (!isNewCart)
                    {
                        _unitOfWork.CartRepository.Update(cart);
                    }

                    // 7. Guardar cambios - EF maneja transacción automática (Cart + CartItem)
                    // ✅ Compatible con RetryPolicy
                    // ✅ Rollback automático si algo falla
                    // ✅ Commit automático si todo sale bien
                    await _unitOfWork.Complete();

                    _logger.LogInformation("✅ Producto agregado al carrito exitosamente para usuario {UserId}", currentUserId);

                    return new AddToCartResponse
                    {
                        Success = true,
                        Message = "Producto agregado al carrito exitosamente",
                        ProductId = productIdGuid,
                        AvailableStock = availableStock,
                        RequestedQuantity = requestedQuantity,
                        ProductName = productDetails.Name,
                        ProductImageUrl = productDetails.Images?.FirstOrDefault()?.ImageUrl,
                        Subtotal = productDetails.Price * requestedQuantity
                    };
                }
                catch (Exception ex)
                {
                    // 🔥 EF ya hizo rollback automático de todas las entidades
                    _logger.LogError(ex, "❌ Error al agregar producto {ProductId} al carrito para usuario {UserId}",
                        request.ProductId, UserId ?? "Unknown");

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