using Catalog.Domain.Common;
using System.Linq.Expressions;

namespace Catalog.Application.Contracts.Persistence
{
    public interface IAsyncRepository<T> where T : BaseAuditableEntity
    {
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IReadOnlyList<T>> GetAsync();
        Task<T> GetById(Guid id);
        void AddEntity(T entity);
        void AddRange(IEnumerable<T> entities);
        Task BulkAddEntity(List<T> entity);
        void UpdateEntity(T entity);
        Task BulkUpdateEntity(List<T> list);
        void DeleteEntity(T entity);

        void DeleteRange(IEnumerable<T> entities);
        Task BulkDeleteEntity(List<T> list);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                       Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                       string includeString = null,
                                       bool disableTracking = true);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                     Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                     bool disableTracking = true,
                                     params Expression<Func<T, object>>[] includes);
    }
}
