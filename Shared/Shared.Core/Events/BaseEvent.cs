namespace Shared.Core.Events
{
    public abstract class BaseEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string EventType => GetType().Name;
    }
}
