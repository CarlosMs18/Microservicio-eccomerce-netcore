using Cart.Application.Contracts.Persistence;
using Cart.Domain;
using Cart.Infrastructure.Persistence;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cart.Infrastructure.Repositories
{
    public class CartItemRepository : RepositoryBase<CartItem>, ICartItemRepository
    {
        public CartItemRepository(CartDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
    }
}