namespace Cart.Application.DTos.External
{
    public class CatalogProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }      
        public bool IsActive { get; set; }        
        public bool HasStock { get; set; }        
        public Guid CategoryId { get; set; }      
        public string? ImageUrl { get; set; }   
    }
}
