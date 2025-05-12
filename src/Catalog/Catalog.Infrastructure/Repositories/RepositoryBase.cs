using Catalog.Application.Contracts.Persistence;
using Catalog.Domain.Common;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using System.Linq.Expressions;

namespace Catalog.Infrastructure.Repositories
{
    public class RepositoryBase<T> : IAsyncRepository<T> where T : BaseAuditableEntity
    {
        protected readonly CatalogDbContext _context;
        public RepositoryBase(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<T>> GetAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public void AddEntity(T entity)
        {
            _context.Set<T>().Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            _context.Set<T>().AddRange(entities);
        }
        public void UpdateEntity(T entity)
        {
            //_context.Set<T>().Attach(entity);
            _context.Set<T>().Update(entity);
        }

        public void DeleteRange(IEnumerable<T> entities)
        {
            _context.Set<T>().RemoveRange(entities);
        }
        public void DeleteEntity(T entity)
        {
            _context.Set<T>().Remove(entity);
        }
        public async Task<T> GetById(Guid id)
        {
            return await _context.Set<T>().FindAsync(id);
        }
        public async Task BulkUpdateEntity(List<T> list)
        {
            await _context.BulkUpdateAsync(list);
        }
        public async Task BulkAddEntity(List<T> entity)
        {
            await _context.BulkInsertAsync(entity);
        }
        public async Task BulkDeleteEntity(List<T> list)
        {
            await _context.BulkDeleteAsync(list);
        }
        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                       Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                       string includeString = null,
                                       bool disableTracking = true)
        {
            IQueryable<T> query = _context.Set<T>();
            if (disableTracking) query = query.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(includeString)) query = query.Include(includeString);

            if (predicate != null) query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();


            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                     Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                     bool disableTracking = true,
                                     params Expression<Func<T, object>>[] includes)
        {

            IQueryable<T> query = _context.Set<T>();
            if (disableTracking) query = query.AsNoTracking();

            if (includes != null) query = includes.Aggregate(query, (current, include) => current.Include(include));

            if (predicate != null) query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();


            return await query.ToListAsync();
        }
    }
}
