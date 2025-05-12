using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Application.Constants;
using User.Application.Contracts.Services;
using User.Application.Models;

namespace User.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly JwtSettings _jwtSettings;
        private readonly UserManager<ApplicationUser> _userManager; 

        public AuthService(SignInManager<ApplicationUser> signInManager , IOptions<JwtSettings> jwtSettings, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<string> GenerateJwtTokenAsync(ApplicationUser user, double durationInMinutes = 0)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(CustomClaimTypes.UID, user.Id)
        }.Union(userClaims).Union(roleClaims);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(durationInMinutes > 0 ? durationInMinutes : _jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<SignInResult> PasswordSignInAsync(string email, string password, bool isPersistent, bool lockoutOnFailure)
        {
            return await _signInManager.PasswordSignInAsync(
                email,
                password,
                isPersistent,
                lockoutOnFailure
            );
        }

       
    }
}
