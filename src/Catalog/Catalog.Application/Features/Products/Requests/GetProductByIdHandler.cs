using Catalog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Shared.Core.Handlers;
using Catalog.Application.DTOs.Responses;

namespace Catalog.Application.Features.Products.Queries
{
    // Query Request
    public class GetProductByIdQuery : IRequest<GetProductByIdResponse>
    {
        public Guid ProductId { get; set; }
    }

    public class GetProductByIdResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProductDetailDto? Product { get; set; }
    }

    public class GetProductByIdHandler : BaseHandler, IRequestHandler<GetProductByIdQuery, GetProductByIdResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetProductByIdHandler> _logger;

        public GetProductByIdHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetProductByIdHandler> logger,
            IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<GetProductByIdResponse> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation($"Obteniendo producto {request.ProductId}. Usuario: {UserId}");

                // ✅ CORRECCIÓN: Pasar los includes como parámetros separados (params)
                var products = await _unitOfWork.ProductRepository.GetWithIncludesAsync(
                    predicate: p => p.Id == request.ProductId,
                    orderBy: null,
                    disableTracking: true,
                    p => p.Category,    // ✅ Primer include como parámetro separado
                    p => p.Images       // ✅ Segundo include como parámetro separado
                );

                var product = products.FirstOrDefault();

                if (product == null)
                {
                    _logger.LogWarning($"Producto {request.ProductId} no encontrado");
                    return new GetProductByIdResponse
                    {
                        Success = false,
                        Message = "Producto no encontrado"
                    };
                }

                // Mapear a DTO detallado
                var productDto = new ProductDetailDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    CategoryId = product.CategoryId,
                    Category = product.Category != null
                        ? new CategoryDto
                        {
                            Id = product.Category.Id,
                            Name = product.Category.Name,
                            Description = product.Category.Description
                        }
                        : null,
                    IsActive = product.IsActive,
                    Stock = product.Stock,
                    CreatedDate = product.CreatedDate,
                    CreatedBy = product.CreatedBy,
                    UpdatedDate = product.UpdatedDate,
                    UpdatedBy = product.UpdatedBy,
                    Images = product.Images != null
                        ? product.Images.Select(img => new ProductImageDto
                        {
                            Id = img.Id,
                            ImageUrl = img.ImageUrl,
                            ProductId = img.ProductId,
                            CreatedDate = img.CreatedDate,
                            CreatedBy = img.CreatedBy,
                            UpdatedDate = img.UpdatedDate,
                            UpdatedBy = img.UpdatedBy
                        }).OrderBy(img => img.CreatedDate).ToList()
                        : new List<ProductImageDto>()
                };

                _logger.LogInformation($"Producto {request.ProductId} obtenido correctamente con {productDto.Images.Count} imágenes");

                return new GetProductByIdResponse
                {
                    Success = true,
                    Message = "Producto obtenido correctamente",
                    Product = productDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener producto {request.ProductId}");
                return new GetProductByIdResponse
                {
                    Success = false,
                    Message = "Error interno al obtener producto"
                };
            }
        }
    }
}