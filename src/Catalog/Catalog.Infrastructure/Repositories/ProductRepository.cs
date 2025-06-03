using Catalog.Application.Contracts.Persistence;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;

namespace Catalog.Infrastructure.Repositories
{
    public class ProductRepository : RepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(CatalogDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
        public async Task<Product?> GetProductWithDetailsAsync(Guid productId)
        {
            return await _context.Products
                .Include(p => p.Category)           
                .Include(p => p.Images)             
                .FirstOrDefaultAsync(p => p.Id == productId);
        }
    }
}
