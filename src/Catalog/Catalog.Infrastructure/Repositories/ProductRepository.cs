using Catalog.Application.Contracts.Persistence;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using Polly.Retry;

namespace Catalog.Infrastructure.Repositories
{
    public class ProductRepository : RepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(CatalogDbContext context, AsyncRetryPolicy retryPolicy) : base(context, retryPolicy)
        {
        }
    }
}
