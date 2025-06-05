using Catalog.Application.Features.Products.Queries;

namespace Catalog.Application.DTOs.Responses
{
    public class ProductDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public Guid CategoryId { get; set; }
        public CategoryDto? Category { get; set; }
        public bool IsActive { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
    }
}
