using Cart.Domain.Common;


namespace Cart.Application.Contracts.Persistence
{
    public interface IUnitOfWork : IDisposable
    {
        ICartItemRepository CartItemRepository { get; }
        ICartRepository CartRepository { get; } 
        IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : BaseAuditableEntity;
        Task<int> Complete();
        Task<int> ExecStoreProcedure(string sql, params object[] parameters);
        Task Rollback();
        Task Commit();
        Task BeginTransaction();
    }
}
