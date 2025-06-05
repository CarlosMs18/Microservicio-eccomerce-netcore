using Cart.Application.Contracts.Persistence;
using Cart.Domain;
using Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;

namespace Cart.Infrastructure.Repositories
{
    public class CartItemRepository : RepositoryBase<CartItem>, ICartItemRepository
    {
        public CartItemRepository(CartDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
        public async Task<Domain.CartItem?> GetByCartAndProductAsync(Guid cartId, Guid productId)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<Domain.CartItem>()
                    .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId));
        }

        public async Task<IReadOnlyList<Domain.CartItem>> GetItemsByCartIdAsync(Guid cartId)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<Domain.CartItem>()
                    .Where(ci => ci.CartId == cartId)
                    .AsNoTracking()
                    .ToListAsync());
        }
    }
}