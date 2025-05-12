using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using Catalog.Domain.Common;

namespace Catalog.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private Hashtable _repositories;
        private readonly CatalogDbContext _context;
        private IDbContextTransaction transaction;
        public ICategoryRepository categoryRepository;
        public UnitOfWork(CatalogDbContext context)
        {
            _context = context;

        }

        public CatalogDbContext catalogDbContext => _context;
        public ICategoryRepository CategoryRepository => categoryRepository ??= new CategoryRepository(_context);
     

        public async Task BeginTransaction()
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task Commit()
        {
            await transaction.CommitAsync();
        }

        public async Task<int> Complete()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine(ex.Message);
                throw new Exception("Error en UnitOfWork: " + ex.InnerException?.Message, ex);
                throw new Exception(ex.Message);
            }
        }


        public void Dispose()
        {
            transaction?.Dispose();
            _context.Dispose();
        }

        public async Task<int> ExecStoreProcedure(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }

        public IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : BaseAuditableEntity
        {
            if (_repositories == null)
            {
                _repositories = new Hashtable();
            }

            var type = typeof(TEntity).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(RepositoryBase<>);
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity)), _context);
                _repositories.Add(type, repositoryInstance);
            }

            return (IAsyncRepository<TEntity>)_repositories[type];
        }

        public async Task Rollback()
        {
            await transaction.RollbackAsync();
        }
    }
}
