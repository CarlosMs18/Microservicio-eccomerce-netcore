using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using User.Application.Contracts.Persistence;
using User.Infrastructure.Persistence;


namespace User.Infrastructure.Repositories
{
    public class RepositoryBase<T> : IAsyncRepository<T> where T : class
    {
        protected readonly UserIdentityDbContext _identityDbContext;

        public RepositoryBase( UserIdentityDbContext identityDbContext)
        {
            _identityDbContext = identityDbContext;
        }
        public async Task<IReadOnlyList<T>> GetAsync()
        {
            return await _identityDbContext.Set<T>().ToListAsync();
        }

        public void AddEntity(T entity)
        {
            _identityDbContext.Set<T>().Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            _identityDbContext.Set<T>().AddRange(entities);
        }
        public void UpdateEntity(T entity)
        {
            //_context.Set<T>().Attach(entity);
            _identityDbContext.Entry(entity).State = EntityState.Modified;
        }

        public void DeleteRange(IEnumerable<T> entities)
        {
            _identityDbContext.Set<T>().RemoveRange(entities);
        }
        public void DeleteEntity(T entity)
        {
            _identityDbContext.Set<T>().Remove(entity);
        }
        public async Task<T> GetById(int id)
        {
            return await _identityDbContext.Set<T>().FindAsync(id);
        }
        public async void BulkUpdateEntity(List<T> list)
        {
            await _identityDbContext.BulkUpdateAsync(list);
        }
        public async void BulkAddEntity(List<T> entity)
        {
            await _identityDbContext.BulkInsertAsync(entity);
        }
        public async void BulkDeleteEntity(List<T> list)
        {
            await _identityDbContext.BulkDeleteAsync(list);
        }
        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _identityDbContext.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                       Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
        string includeString = null,
                                       bool disableTracking = true)
        {
            IQueryable<T> query = _identityDbContext.Set<T>();
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

            IQueryable<T> query = _identityDbContext.Set<T>();
            if (disableTracking) query = query.AsNoTracking();

            if (includes != null) query = includes.Aggregate(query, (current, include) => current.Include(include));

            if (predicate != null) query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();


            return await query.ToListAsync();
        }
    }
}
