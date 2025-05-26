using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Collections;
using User.Application.Contracts.Persistence;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly UserIdentityDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly IUserRepository _userRepository;
        private Hashtable _repositories;
        private IDbContextTransaction _transaction;

        public UnitOfWork(
            UserIdentityDbContext context,
            ILogger<UnitOfWork> logger,
            IUserRepository userRepository)
        {
            _context = context;
            _logger = logger;
            _userRepository = userRepository;
            _repositories = new Hashtable();
        }

        public IUserRepository UserRepository => _userRepository;

        // ==================== TRANSACCIONES ====================
        public async Task BeginTransaction()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task Commit()
        {
            if (_transaction == null)
                throw new InvalidOperationException("No hay transacción activa");

            await _transaction.CommitAsync();
        }

        public async Task Rollback()
        {
            if (_transaction != null)
                await _transaction.RollbackAsync();
        }

        // ==================== ESCRITURAS (SIN RETRY) ====================
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

        public async Task<int> ExecStoreProcedure(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }

        // ==================== LECTURAS ====================
        public IAsyncRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            if (_repositories == null)
            {
                _repositories = new Hashtable();
            }

            var type = typeof(TEntity).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(RepositoryBase<>);
                var repositoryInstance = Activator.CreateInstance(
                    repositoryType.MakeGenericType(typeof(TEntity)),
                    _context);
                _repositories.Add(type, repositoryInstance);
            }

            return (IAsyncRepository<TEntity>)_repositories[type];
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}