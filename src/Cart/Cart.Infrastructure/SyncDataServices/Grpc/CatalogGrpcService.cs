using Catalog.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Cart.Application.Contracts.External;
using Cart.Application.DTos.External;

namespace Cart.Infrastructure.SyncDataServices.Grpc
{
    public class CatalogGrpcService : ICatalogService
    {
        private readonly CatalogService.CatalogServiceClient _client;
        private readonly ILogger<CatalogGrpcService> _logger;

        public CatalogGrpcService(
            CatalogService.CatalogServiceClient client,
            ILogger<CatalogGrpcService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<bool> ProductExistsAsync(Guid productId)
        {
            try
            {
                _logger.LogDebug("🔍 Verificando existencia del producto {ProductId} via gRPC", productId);

                var request = new ProductExistsRequest
                {
                    ProductId = productId.ToString() 
                };

                var response = await _client.CheckProductExistsAsync(request);

                _logger.LogInformation("✅ Producto {ProductId} existe: {Exists} - {Message}",
                    productId, response.Exists, response.Message);

                return response.Exists;
            }
            catch (RpcException rpcEx)
            {
                _logger.LogError(rpcEx, "❌ Error gRPC al verificar producto {ProductId}: {Status} - {Detail}",
                    productId, rpcEx.StatusCode, rpcEx.Status.Detail);

                // Decidir según el tipo de error
                return rpcEx.StatusCode switch
                {
                    StatusCode.NotFound => false,
                    StatusCode.InvalidArgument => false,
                    _ => throw new Exception($"Error al verificar producto: {rpcEx.Status.Detail}", rpcEx)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado al verificar producto {ProductId}", productId);
                throw new Exception($"Error al verificar producto {productId}", ex);
            }
        }

        public async Task<int> GetProductStockAsync(Guid productId)
        {
            try
            {
                _logger.LogDebug("📦 Obteniendo stock del producto {ProductId} via gRPC", productId);

                var request = new ProductStockRequest
                {
                    ProductId = productId.ToString() // Conversión de Guid a string para gRPC
                };

                var response = await _client.GetProductStockAsync(request);

                if (!response.Exists)
                {
                    _logger.LogWarning("🚫 Producto {ProductId} no existe o está inactivo: {Message}",
                        productId, response.Message);
                    return 0; // O lanzar excepción según tu lógica de negocio
                }

                _logger.LogInformation("📊 Stock del producto {ProductId}: {Stock} - {Message}",
                    productId, response.Stock, response.Message);

                return response.Stock;
            }
            catch (RpcException rpcEx)
            {
                _logger.LogError(rpcEx, "❌ Error gRPC al obtener stock del producto {ProductId}: {Status} - {Detail}",
                    productId, rpcEx.StatusCode, rpcEx.Status.Detail);

                // Decidir según el tipo de error
                return rpcEx.StatusCode switch
                {
                    StatusCode.NotFound => 0,
                    StatusCode.InvalidArgument => 0,
                    _ => throw new Exception($"Error al obtener stock: {rpcEx.Status.Detail}", rpcEx)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado al obtener stock del producto {ProductId}", productId);
                throw new Exception($"Error al obtener stock del producto {productId}", ex);
            }
        }

        public async Task<ProductDetailsDto> GetProductDetailsAsync(Guid productId)
        {
            try
            {
                _logger.LogDebug("🔍 Obteniendo detalles del producto {ProductId} via gRPC", productId);

                var request = new ProductDetailsRequest
                {
                    ProductId = productId.ToString()
                };

                var response = await _client.GetProductDetailsAsync(request);

                if (!response.Exists)
                {
                    _logger.LogWarning("🚫 Producto {ProductId} no existe o está inactivo: {Message}",
                        productId, response.Message);

                    return null; // O puedes lanzar una excepción según tu lógica
                }

                _logger.LogInformation("✅ Detalles del producto {ProductId} obtenidos exitosamente",
                    productId);

                // Mapear de gRPC response a DTO
                var productDto = new ProductDetailsDto
                {
                    Id = Guid.Parse(response.Product.Id),
                    Name = response.Product.Name,
                    Description = response.Product.Description,
                    Price = (decimal)response.Product.Price,
                    IsActive = response.Product.IsActive,
                    Stock = response.Product.Stock,
                    Category = new CategoryDto
                    {
                        Id = Guid.Parse(response.Product.Category.Id),
                        Name = response.Product.Category.Name,
                        Description = response.Product.Category.Description
                    },
                    Images = response.Product.Images.Select(img => new ProductImageDto
                    {
                        Id = Guid.Parse(img.Id),
                        ImageUrl = img.ImageUrl
                    }).ToList()
                };

                return productDto;
            }
            catch (RpcException rpcEx)
            {
                _logger.LogError(rpcEx, "❌ Error gRPC al obtener detalles del producto {ProductId}: {Status} - {Detail}",
                    productId, rpcEx.StatusCode, rpcEx.Status.Detail);

                return rpcEx.StatusCode switch
                {
                    StatusCode.NotFound => null,
                    StatusCode.InvalidArgument => null,
                    _ => throw new Exception($"Error al obtener detalles del producto: {rpcEx.Status.Detail}", rpcEx)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado al obtener detalles del producto {ProductId}", productId);
                throw new Exception($"Error al obtener detalles del producto {productId}", ex);
            }
        }
    }
}