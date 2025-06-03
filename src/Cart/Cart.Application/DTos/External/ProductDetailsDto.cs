namespace Cart.Application.DTos.External
{
    public class ProductDetailsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int Stock { get; set; }
        public CategoryDto Category { get; set; } = new();
        public List<ProductImageDto> Images { get; set; } = new();
    }
}
