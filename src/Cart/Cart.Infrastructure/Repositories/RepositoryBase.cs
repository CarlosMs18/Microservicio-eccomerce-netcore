using Cart.Application.Contracts.Persistence;
using Cart.Domain.Common;
using Cart.Infrastructure.Persistence;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;
using System.Linq.Expressions;

namespace Cart.Infrastructure.Repositories
{
    public class RepositoryBase<T> : IAsyncRepository<T> where T : BaseAuditableEntity
    {
        protected readonly CartDbContext _context;
        protected readonly AsyncRetryPolicy _retryPolicy;
        public RepositoryBase(CartDbContext context, AsyncRetryPolicy retryPolicy)
        {
            _context = context;
            _retryPolicy = retryPolicy;
        }

        // ==================== OPERACIONES DE LECTURA (con retry) ====================
        public async Task<IReadOnlyList<T>> GetAsync()
            => await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<T>().AsNoTracking().ToListAsync());

        public async Task<T> GetByIdAsync(Guid id)
            => await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<T>().FindAsync(id));

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
            => await _retryPolicy.ExecuteAsync(async () =>
                await _context.Set<T>().Where(predicate).AsNoTracking().ToListAsync());

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            string includeString = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = _context.Set<T>();
            if (disableTracking) query = query.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(includeString)) query = query.Include(includeString);
            if (predicate != null) query = query.Where(predicate);
            return orderBy != null
                ? await orderBy(query).ToListAsync()
                : await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            bool disableTracking = true,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();
            if (disableTracking) query = query.AsNoTracking();
            if (includes != null)
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            if (predicate != null) query = query.Where(predicate);
            return orderBy != null
                ? await orderBy(query).ToListAsync()
                : await query.ToListAsync();
        }

        // ==================== OPERACIONES BÁSICAS (sin guardar) ====================
        public void Add(T entity)
            => _context.Set<T>().Add(entity);

        public void Update(T entity)
            => _context.Entry(entity).State = EntityState.Modified;

        public void Delete(T entity)
            => _context.Set<T>().Remove(entity);

        // ==================== OPERACIONES CON GUARDADO ====================
        public async Task AddAndSaveAsync(T entity)
        {
            _context.Set<T>().Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAndSaveAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAndSaveAsync(T entity)
        {
            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync();
        }

        // ==================== OPERACIONES MASIVAS ====================
        public async Task BulkInsertAsync(List<T> entities)
            => await _context.BulkInsertAsync(entities);

        public async Task BulkUpdateAsync(List<T> entities)
            => await _context.BulkUpdateAsync(entities);

        public async Task BulkDeleteAsync(List<T> entities)
            => await _context.BulkDeleteAsync(entities);
    }
}
