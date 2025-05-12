using Catalog.Domain;

namespace Catalog.Application.Contracts.Persistence
{
    public interface ICategoryRepository : IAsyncRepository<Category>
    {
        Task<bool> ExistsByNameAsync(string name);
    }
}
