namespace Cart.Application.Contracts.Persistence
{
    public interface ICartRepository : IAsyncRepository<Cart.Domain.Cart>
    {
    }
}
