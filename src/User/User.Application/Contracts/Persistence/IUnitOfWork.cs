namespace User.Application.Contracts.Persistence
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepository { get; } 
        IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : class;
        Task<int> Complete();
        Task<int> ExecStoreProcedure(string sql, params object[] parameters);
        Task Rollback();
        Task Commit();
        Task BeginTransaction();
    }
}
