using Catalog.Application.Contracts.Persistence;
using Catalog.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Catalog.Infrastructure.Services.External.Grpc
{
    public class CatalogGrpcService : CatalogService.CatalogServiceBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CatalogGrpcService> _logger;

        public CatalogGrpcService(IUnitOfWork unitOfWork, ILogger<CatalogGrpcService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public override async Task<ProductExistsResponse> CheckProductExists(
            ProductExistsRequest request,
            ServerCallContext context)
        {
            try
            {
                Console.WriteLine("ho");
                // 1. VALIDACIÓN DE INPUT - Mejor práctica
                var validationResult = ValidateProductId(request.ProductId);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("❌ ProductId inválido: {ProductId} - {Error}",
                        request.ProductId, validationResult.ErrorMessage);

                    throw new RpcException(new Status(StatusCode.InvalidArgument, validationResult.ErrorMessage));
                }

                var productId = validationResult.ProductId;
                _logger.LogDebug("🔍 Verificando existencia del producto {ProductId}", productId);

                var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId);

                if (product == null)
                {
                    _logger.LogInformation("🚫 Producto {ProductId} no encontrado", productId);
                    return new ProductExistsResponse
                    {
                        Exists = false,
                        Message = "Producto no encontrado"
                    };
                }

                _logger.LogInformation("✅ Producto {ProductId} existe y está {Status}",
                    productId, product.IsActive ? "activo" : "inactivo");

                return new ProductExistsResponse
                {
                    Exists = product.IsActive,
                    Message = product.IsActive ? "Producto encontrado" : "Producto inactivo"
                };
            }
            catch (RpcException)
            {
                // Re-lanzar RpcException tal como está
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error interno al verificar producto {ProductId}", request.ProductId);
                throw new RpcException(new Status(StatusCode.Internal, "Error interno del servidor"));
            }
        }

        public override async Task<ProductStockResponse> GetProductStock(
            ProductStockRequest request,
            ServerCallContext context)
        {
            try
            {
                Console.WriteLine("ho2");
                // 1. VALIDACIÓN DE INPUT - Mejor práctica
                var validationResult = ValidateProductId(request.ProductId);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("❌ ProductId inválido: {ProductId} - {Error}",
                        request.ProductId, validationResult.ErrorMessage);

                    throw new RpcException(new Status(StatusCode.InvalidArgument, validationResult.ErrorMessage));
                }

                var productId = validationResult.ProductId;
                _logger.LogDebug("📦 Obteniendo stock del producto {ProductId}", productId);

                var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId);

                if (product == null)
                {
                    _logger.LogInformation("🚫 Producto {ProductId} no encontrado", productId);
                    return new ProductStockResponse
                    {
                        Exists = false,
                        Stock = 0,
                        Message = "Producto no encontrado"
                    };
                }

                if (!product.IsActive)
                {
                    _logger.LogInformation("⚠️ Producto {ProductId} existe pero está inactivo", productId);
                    return new ProductStockResponse
                    {
                        Exists = false,
                        Stock = 0,
                        Message = "Producto inactivo"
                    };
                }

                _logger.LogInformation("📊 Producto {ProductId} - Stock disponible: {Stock}",
                    productId, product.Stock);

                return new ProductStockResponse
                {
                    Exists = true,
                    Stock = product.Stock,
                    Message = product.Stock > 0 ? "Stock disponible" : "Sin stock disponible"
                };
            }
            catch (RpcException)
            {
                // Re-lanzar RpcException tal como está
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error interno al obtener stock del producto {ProductId}", request.ProductId);
                throw new RpcException(new Status(StatusCode.Internal, "Error interno del servidor"));
            }
        }

        // 2. MÉTODO DE VALIDACIÓN CENTRALIZADO - Mejor práctica
        private static ProductIdValidationResult ValidateProductId(string productIdString)
        {
            if (string.IsNullOrWhiteSpace(productIdString))
            {
                return ProductIdValidationResult.Invalid("El ID del producto es requerido");
            }

            if (!Guid.TryParse(productIdString, out var productId))
            {
                return ProductIdValidationResult.Invalid("El formato del ID del producto es inválido");
            }

            if (productId == Guid.Empty)
            {
                return ProductIdValidationResult.Invalid("El ID del producto no puede estar vacío");
            }

            return ProductIdValidationResult.Valid(productId);
        }
    }

    // 3. CLASE PARA VALIDACIÓN - Compatible con .NET 7
    public class ProductIdValidationResult
    {
        public bool IsValid { get; set; }
        public Guid ProductId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // Factory methods para crear instancias
        public static ProductIdValidationResult Valid(Guid productId)
        {
            return new ProductIdValidationResult
            {
                IsValid = true,
                ProductId = productId,
                ErrorMessage = string.Empty
            };
        }

        public static ProductIdValidationResult Invalid(string errorMessage)
        {
            return new ProductIdValidationResult
            {
                IsValid = false,
                ProductId = Guid.Empty,
                ErrorMessage = errorMessage
            };
        }
    }
}