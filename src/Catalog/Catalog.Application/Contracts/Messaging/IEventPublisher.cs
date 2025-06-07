namespace Catalog.Application.Contracts.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : class;
    }
}
