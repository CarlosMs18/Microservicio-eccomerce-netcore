using Cart.Application.Contracts.Persistence;
using Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;

namespace Cart.Infrastructure.Repositories
{
    public class CartRepository : RepositoryBase<Cart.Domain.Cart>, ICartRepository
    {
        public CartRepository(CartDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }

        public async Task<Domain.Cart?> GetCartByUserIdAsync(string userId)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<Domain.Cart>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CreatedBy == userId));
        }

        public async Task<Domain.Cart?> GetCartWithItemsByUserIdAsync(string userId)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<Domain.Cart>()
                    .Include(c => c.Items)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CreatedBy == userId));
        }
    }
}
