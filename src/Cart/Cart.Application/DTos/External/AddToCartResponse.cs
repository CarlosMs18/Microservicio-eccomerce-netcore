namespace Cart.Application.DTos.External
{
    public class AddToCartResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid ProductId { get; set; }
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
    }
}
