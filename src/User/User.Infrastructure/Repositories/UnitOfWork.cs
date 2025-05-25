using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using System.Collections;
using User.Application.Contracts.Persistence;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private Hashtable _repositories;
        private readonly UserIdentityDbContext _userIdentityDbContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnitOfWork> _logger;
        private IDbContextTransaction _transaction;
        private IUserRepository _userRepository;

        // Constructor: Elimina AsyncRetryPolicy de aquí (no se usa en UnitOfWork)
        public UnitOfWork(
            UserIdentityDbContext userIdentityDbContext,
            ILogger<UnitOfWork> logger)
        {
            _userIdentityDbContext = userIdentityDbContext;
            _logger = logger;
            _repositories = new Hashtable();
        }

        public UserIdentityDbContext userIdentityDbContext => _userIdentityDbContext;
        public IUserRepository UserRepository => _userRepository ??=
                        _serviceProvider.GetRequiredService<IUserRepository>();

        public async Task BeginTransaction()
        {
            _transaction = await _userIdentityDbContext.Database.BeginTransactionAsync();
        }

        public async Task Commit()
        {
            await _transaction.CommitAsync();
        }

        // Complete SIN Polly (para escrituras)
        public async Task<int> Complete()
        {
            try
            {
                return await _userIdentityDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UnitOfWork.Complete");
                throw; // Propaga el error para manejo en capa superior
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _userIdentityDbContext.Dispose();
        }

        // ExecStoreProcedure SIN Polly (usa try/catch manual si es crítico)
        public async Task<int> ExecStoreProcedure(string sql, params object[] parameters)
        {
            return await _userIdentityDbContext.Database.ExecuteSqlRawAsync(sql, parameters);
        }

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
                    _userIdentityDbContext);
                _repositories.Add(type, repositoryInstance);
            }

            return (IAsyncRepository<TEntity>)_repositories[type];
        }

        public async Task Rollback()
        {
            await _transaction.RollbackAsync();
        }
    }
}