
using Cart.Domain;

namespace Cart.Application.Contracts.Persistence
{
    public interface ICartItemRepository : IAsyncRepository<CartItem>
    {
    }
}
