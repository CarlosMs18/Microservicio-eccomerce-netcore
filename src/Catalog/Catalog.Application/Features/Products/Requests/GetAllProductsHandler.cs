using Catalog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Shared.Core.Handlers;
using Catalog.Application.DTOs.Responses;

namespace Catalog.Application.Features.Products.Queries
{
    // ============= GET ALL PRODUCTS (CON IMÁGENES) =============
    public class GetAllProductsQuery : IRequest<GetAllProductsResponse>
    {
        // Sin parámetros - comportamiento fijo y predecible
    }

    public class GetAllProductsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ProductDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class GetAllProductsHandler : BaseHandler, IRequestHandler<GetAllProductsQuery, GetAllProductsResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAllProductsHandler> _logger;

        public GetAllProductsHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetAllProductsHandler> logger,
            IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<GetAllProductsResponse> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation($"Obteniendo todos los productos con imágenes. Usuario: {UserId}");

                // ✅ USAR GetWithIncludesAsync para incluir Category e Images
                var products = await _unitOfWork.ProductRepository.GetWithIncludesAsync(
                    predicate: p => p.IsActive,
                    orderBy: q => q.OrderBy(p => p.Name),
                    disableTracking: true,
                    p => p.Category,    // ✅ Include Category
                    p => p.Images       // ✅ Include Images
                );

                // Mapear a DTOs CON IMÁGENES
                var productDtos = products.Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category?.Name ?? "Sin categoría",
                    IsActive = p.IsActive,
                    Stock = p.Stock,
                    CreatedDate = p.CreatedDate,
                    CreatedBy = p.CreatedBy,
                    UpdatedDate = p.UpdatedDate,
                    UpdatedBy = p.UpdatedBy,
                    // ✅ MAPEAR LAS IMÁGENES CORRECTAMENTE
                    Images = p.Images != null && p.Images.Any()
                        ? p.Images.Select(img => new ProductImageDto
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
                }).ToList();

                _logger.LogInformation($"Se obtuvieron {productDtos.Count} productos con sus imágenes");

                return new GetAllProductsResponse
                {
                    Success = true,
                    Message = "Productos obtenidos correctamente",
                    Products = productDtos,
                    TotalCount = productDtos.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los productos");
                return new GetAllProductsResponse
                {
                    Success = false,
                    Message = "Error interno al obtener productos"
                };
            }
        }
    }
}