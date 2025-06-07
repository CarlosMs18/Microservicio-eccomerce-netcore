namespace Shared.Core.Events
{
    public class ProductPriceChangedEvent : BaseEvent
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
    }
}
