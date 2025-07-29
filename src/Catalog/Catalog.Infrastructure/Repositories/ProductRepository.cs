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

        public async Task<Product?> GetByNameAsync(string name)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var normalizedName = name.Trim().ToLower();
                return await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Name.Trim().ToLower() == normalizedName);
            });
        }
    }
}
