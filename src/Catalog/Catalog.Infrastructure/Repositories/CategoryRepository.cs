using Catalog.Application.Contracts.Persistence;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;

namespace Catalog.Infrastructure.Repositories
{
    public class CategoryRepository: RepositoryBase<Category>, ICategoryRepository
    {
        public CategoryRepository(
            CatalogDbContext context,
            AsyncRetryPolicy retryPolicy
            ) : base(context, retryPolicy)
        {
            
        }

      
         public async Task<bool> ExistsByNameAsync(string name)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Name.Trim().ToLower() == normalizedName);
        }
    }
}
