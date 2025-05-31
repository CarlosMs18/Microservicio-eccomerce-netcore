using Cart.Application.Contracts.Persistence;
using Cart.Domain.Common;
using Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Collections;

namespace Cart.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly CartDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly ICartRepository _cartRepository;
        private Hashtable _repositories;
        private IDbContextTransaction _transaction;

        public UnitOfWork(
            CartDbContext context,
            ILogger<UnitOfWork> logger,
            ICartItemRepository cartItemRepository,
            ICartRepository cartRepository) // Inyección directa
        {
            _context = context;
            _logger = logger;
            _cartItemRepository = cartItemRepository;
            _cartRepository = cartRepository;   
            _repositories = new Hashtable();
        }

        public CartDbContext CatalogDbContext => _context;
        public ICartRepository CartRepository => _cartRepository; // Usa la instancia inyectada

        public ICartItemRepository CartItemRepository => _cartItemRepository;

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
