using Catalog.Domain.Common;
using System.Linq.Expressions;

namespace Catalog.Application.Contracts.Persistence
{
    public interface IAsyncRepository<T> where T : BaseAuditableEntity
    {
        Task<IReadOnlyList<T>> GetAsync();
        Task<T> GetByIdAsync(string id);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            string includeString = null,
            bool disableTracking = true);
        Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            bool disableTracking = true,
            params Expression<Func<T, object>>[] includes);

        // Operaciones básicas (para UnitOfWork)
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);

        // Operaciones con guardado inmediato
        Task AddAndSaveAsync(T entity);
        Task UpdateAndSaveAsync(T entity);
        Task DeleteAndSaveAsync(T entity);

        // Operaciones masivas
        Task BulkInsertAsync(List<T> entities);
        Task BulkUpdateAsync(List<T> entities);
        Task BulkDeleteAsync(List<T> entities);
    }
}
