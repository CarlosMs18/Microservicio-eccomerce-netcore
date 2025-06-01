namespace Cart.Application.DTos.Requests
{
    public class AddProductToCartRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
