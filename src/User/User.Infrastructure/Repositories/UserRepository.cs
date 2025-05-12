using Microsoft.EntityFrameworkCore;
using User.Application.Contracts.Persistence;
using User.Application.Models;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Repositories
{
    public class UserRepository : RepositoryBase<ApplicationUser> , IUserRepository
    {
        public UserRepository(UserIdentityDbContext identityDbContext) : base(identityDbContext)
        {
            
        }

        public async Task<ApplicationUser> GetByEmailWithRolesAsync(string email, bool trackChanges = false)
        {
            var query = _identityDbContext.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.Email == email);

            if (!trackChanges) query = query.AsNoTracking(); // 👈 Solo lectura = más eficiente

            return await query.FirstOrDefaultAsync();
        }

        public async Task<bool> IsEmailUniqueAsync(string email, bool trackChanges = false)
        {
            var query = _identityDbContext.Users
                .Where(x => x.Email == email);

            if (!trackChanges) query = query.AsNoTracking(); // 👈 Mejor performance para validaciones

            return !await query.AnyAsync();
        }
    }
}
