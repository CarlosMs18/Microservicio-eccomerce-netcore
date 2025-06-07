using Cart.Domain.Common;

namespace Cart.Domain
{
    public class Cart : BaseAuditableEntity
    {
        public List<CartItem> Items { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
