using Microsoft.AspNetCore.Identity;
using User.Application.Models;

namespace User.Application.Contracts.Services
{
    public interface IAuthService
    {
        Task<SignInResult> PasswordSignInAsync(string email, string password, bool isPersistent, bool lockoutOnFailure);
        Task<string> GenerateJwtTokenAsync(ApplicationUser user, double durationInMinutes = 0);
       
    }
}
