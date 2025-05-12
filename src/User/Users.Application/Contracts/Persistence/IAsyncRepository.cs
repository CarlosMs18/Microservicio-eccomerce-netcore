using System.Linq.Expressions;

namespace User.Application.Contracts.Persistence
{
    public interface IAsyncRepository<T> where T : class
    {
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IReadOnlyList<T>> GetAsync();
        Task<T> GetById(int id);
        void AddEntity(T entity);
        void AddRange(IEnumerable<T> entities);
        void BulkAddEntity(List<T> entity);
        void UpdateEntity(T entity);
        void BulkUpdateEntity(List<T> list);
        void DeleteEntity(T entity);

        void DeleteRange(IEnumerable<T> entities);
        void BulkDeleteEntity(List<T> list);
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
