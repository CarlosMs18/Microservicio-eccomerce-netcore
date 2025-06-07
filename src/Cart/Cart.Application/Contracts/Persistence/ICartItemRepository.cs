
using Cart.Domain;

namespace Cart.Application.Contracts.Persistence
{
    public interface ICartItemRepository : IAsyncRepository<CartItem>
    {
        Task<Domain.CartItem?> GetByCartAndProductAsync(Guid cartId, Guid productId);
        Task<IReadOnlyList<Domain.CartItem>> GetItemsByCartIdAsync(Guid cartId);
        Task<IReadOnlyList<CartItem>> GetByProductIdAsync(Guid productId);
    }
}
