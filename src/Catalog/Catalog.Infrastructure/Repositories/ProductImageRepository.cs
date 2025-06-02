using Catalog.Application.Contracts.Persistence;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Polly.Retry;

namespace Catalog.Infrastructure.Repositories
{
    public class ProductImageRepository : RepositoryBase<ProductImage>, IProductImageRepository
    {
        public ProductImageRepository(CatalogDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
    }
}
