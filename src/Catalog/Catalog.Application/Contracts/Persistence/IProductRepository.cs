using Catalog.Domain;

namespace Catalog.Application.Contracts.Persistence
{
    public interface IProductRepository : IAsyncRepository<Product>
    {
        Task<Product?> GetProductWithDetailsAsync(Guid productId);
    }
}
