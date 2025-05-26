using Catalog.Application.Contracts.Persistence;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Collections;
using Catalog.Domain.Common;

namespace Catalog.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly ICategoryRepository _categoryRepository;
        private Hashtable _repositories;
        private IDbContextTransaction _transaction;

        public UnitOfWork(
            CatalogDbContext context,
            ILogger<UnitOfWork> logger,
            ICategoryRepository categoryRepository) // Inyección directa
        {
            _context = context;
            _logger = logger;
            _categoryRepository = categoryRepository;
            _repositories = new Hashtable();
        }

        public CatalogDbContext CatalogDbContext => _context;
        public ICategoryRepository CategoryRepository => _categoryRepository; // Usa la instancia inyectada

        public async Task BeginTransaction()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task Commit()
        {
            if (_transaction == null) throw new InvalidOperationException("No hay transacción activa");
            await _transaction.CommitAsync();
        }

        public async Task<int> Complete()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cambios");
                throw;
            }
        }

        public IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : BaseAuditableEntity
        {
            if (_repositories == null) _repositories = new Hashtable();

            var type = typeof(TEntity).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryInstance = Activator.CreateInstance(
                    typeof(RepositoryBase<>).MakeGenericType(typeof(TEntity)),
                    _context);

                _repositories.Add(type, repositoryInstance);
            }

            return (IAsyncRepository<TEntity>)_repositories[type];
        }

        public async Task Rollback()
        {
            if (_transaction != null) await _transaction.RollbackAsync();
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<int> ExecStoreProcedure(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
    }
}