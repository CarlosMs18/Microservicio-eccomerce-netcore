using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections;
using User.Application.Contracts.Persistence;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private Hashtable _repositories;
        private readonly UserIdentityDbContext _userIdentityDbContext;
        private IDbContextTransaction transaction;
        private IUserRepository _userRepository;    

        public UnitOfWork(UserIdentityDbContext userIdentityDbContext)
        {
            _userIdentityDbContext = userIdentityDbContext;

        }

        public UserIdentityDbContext userIdentityDbContext => _userIdentityDbContext;
        public IUserRepository UserRepository => _userRepository ??= new UserRepository(_userIdentityDbContext);

        public async Task BeginTransaction()
        {
            transaction = await _userIdentityDbContext.Database.BeginTransactionAsync();
        }

        public async Task Commit()
        {
            await transaction.CommitAsync();
        }

        public async Task<int> Complete()
        {
            try
            {
                return await _userIdentityDbContext.SaveChangesAsync();
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
            _userIdentityDbContext.Dispose();
        }

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
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity)), _userIdentityDbContext);
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
