using Cart.Application.Contracts.Persistence;
using Cart.Infrastructure.Persistence;
using Polly.Retry;

namespace Cart.Infrastructure.Repositories
{
    public class CartRepository : RepositoryBase<Cart.Domain.Cart>, ICartRepository
    {
        public CartRepository(CartDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
    }
}
