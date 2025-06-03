using Cart.Domain.Common;

namespace Cart.Domain
{
    public class CartItem : BaseAuditableEntity
    {
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }

        // Información del producto (snapshot al momento de agregarlo)
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }

        // Imagen principal del producto
        public string? ProductImageUrl { get; set; }

        // Información de la categoría
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        // Propiedades calculadas
        public decimal Subtotal => Price * Quantity;

        // Relación con el carrito padre
        public Cart Cart { get; set; } = null!;
    }
}
