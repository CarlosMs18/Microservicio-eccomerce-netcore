using Catalog.Domain.Common;

namespace Catalog.Domain
{
    public class ProductImage : BaseAuditableEntity
    {
        public string ImageUrl { get; set; }
        public Guid ProductId { get; set; }
        public Product Product { get; set; }
    }
}
