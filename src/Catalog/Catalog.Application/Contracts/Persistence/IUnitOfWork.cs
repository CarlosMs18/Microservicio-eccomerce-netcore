using Catalog.Domain.Common;

namespace Catalog.Application.Contracts.Persistence
{
    public interface IUnitOfWork : IDisposable
    {
        ICategoryRepository CategoryRepository { get; } 
        IProductImageRepository ProductImageRepository { get; } 
        IProductRepository ProductRepository { get; }   
        IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : BaseAuditableEntity;
        Task<int> Complete();
        Task<int> ExecStoreProcedure(string sql, params object[] parameters);
        Task Rollback();
        Task Commit();
        Task BeginTransaction();
    }
}
