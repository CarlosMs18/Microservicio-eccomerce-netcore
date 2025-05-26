using Grpc.Net.Client.Configuration;
using Microsoft.EntityFrameworkCore;
using Polly.Retry;
using User.Application.Contracts.Persistence;
using User.Application.Models;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Repositories
{
    public class UserRepository : RepositoryBase<ApplicationUser>, IUserRepository
    {
        
        public UserRepository(
            UserIdentityDbContext identityDbContext,
            AsyncRetryPolicy retryPolicy)
            : base(identityDbContext, retryPolicy) // Base ya maneja el contexto y el retryPolicy
        {
          
        }

        public async Task<ApplicationUser> GetByEmailWithRolesAsync(string email, bool trackChanges = false)
        {
            return await _retryPolicy.ExecuteAsync(async () => // 👈 Usa el _retryPolicy heredado
            {
                var query = _context.Users // 👈 Usa _context (heredado de RepositoryBase)
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .Where(u => u.Email == email);

                if (!trackChanges)
                    query = query.AsNoTracking();

                return await query.FirstOrDefaultAsync();
            });
        }

        public async Task<bool> IsEmailUniqueAsync(string email, bool trackChanges = false)
        {
            var query = _context.Users // 👈 Usa _context
                .Where(x => x.Email == email);

            if (!trackChanges)
                query = query.AsNoTracking();

            return !await query.AnyAsync();
        }
    }
}