using Catalog.Domain.Common;

namespace Catalog.Domain
{
    public class Product: BaseAuditableEntity
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public Guid CategoryId { get; set; }
        public Category Category { get; set; }
        public bool IsActive { get; set; }
        public int Stock { get; set; }
        public ICollection<ProductImage> Images { get; set; }
    }
}
