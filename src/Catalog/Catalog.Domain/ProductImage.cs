using Catalog.Domain.Common;

namespace Catalog.Domain
{
    public class ProductImage : BaseAuditableEntity
    {
        public string ImageUrl { get; set; } = null!;
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; } = null!;
    }
}
