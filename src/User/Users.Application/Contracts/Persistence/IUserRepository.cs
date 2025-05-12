using User.Application.Models;

namespace User.Application.Contracts.Persistence
{
    public interface IUserRepository : IAsyncRepository<ApplicationUser>
    {
        Task<ApplicationUser> GetByEmailWithRolesAsync(string email , bool trackChanges = false);
        Task<bool> IsEmailUniqueAsync(string email, bool trackChanges = false);
    }
}
