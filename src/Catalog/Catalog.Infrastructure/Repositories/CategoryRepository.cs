using Catalog.Application.Contracts.Persistence;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories
{
    public class CategoryRepository: RepositoryBase<Category>, ICategoryRepository
    {
        public CategoryRepository(CatalogDbContext context) : base(context)
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
