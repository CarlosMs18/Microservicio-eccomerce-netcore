using Catalog.Domain.Common;

namespace Catalog.Domain
{
    public class Category: BaseAuditableEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
