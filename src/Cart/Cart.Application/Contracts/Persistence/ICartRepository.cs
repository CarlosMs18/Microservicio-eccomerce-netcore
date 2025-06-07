namespace Cart.Application.Contracts.Persistence
{
    public interface ICartRepository : IAsyncRepository<Cart.Domain.Cart>
    {
        Task<Domain.Cart?> GetCartByUserIdAsync(string userId);
        Task<Domain.Cart?> GetCartWithItemsByUserIdAsync(string userId);

    }
}
