using Catalog.Domain.Common;
using System.Linq.Expressions;

namespace Catalog.Application.Contracts.Persistence
{
    public interface IAsyncRepository<T> where T : BaseAuditableEntity
    {
        // ==================== OPERACIONES DE LECTURA ====================
        Task<IReadOnlyList<T>> GetAsync();
        Task<T> GetByIdAsync(Guid id);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);

        // Método con string includes
        Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            string includeString = null,
            bool disableTracking = true);

        // ✅ NUEVO: Método específico con expression includes
        Task<IReadOnlyList<T>> GetWithIncludesAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            bool disableTracking = true,
            params Expression<Func<T, object>>[] includes);

        // ==================== OPERACIONES BÁSICAS ====================
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);

        // ==================== OPERACIONES CON GUARDADO ====================
        Task AddAndSaveAsync(T entity);
        Task UpdateAndSaveAsync(T entity);
        Task DeleteAndSaveAsync(T entity);

        // ==================== OPERACIONES MASIVAS ====================
        Task BulkInsertAsync(List<T> entities);
        Task BulkUpdateAsync(List<T> entities);
        Task BulkDeleteAsync(List<T> entities);
    }
}