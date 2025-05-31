using Cart.Domain.Common;

namespace Cart.Domain
{
    public class CartItem : BaseAuditableEntity
    {
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }  
        public string ProductName { get; set; }  
        public decimal Price { get; set; }     
        public int Quantity { get; set; }
    }
}
