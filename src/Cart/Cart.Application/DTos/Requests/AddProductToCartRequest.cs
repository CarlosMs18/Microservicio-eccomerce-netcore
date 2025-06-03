namespace Cart.Application.DTos.Requests
{
    public class AddProductToCartRequest
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
