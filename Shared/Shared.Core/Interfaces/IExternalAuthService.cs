using Shared.Core.Dtos;

namespace Shared.Core.Interfaces
{
    public interface IExternalAuthService
    {
        Task<TokenValidationDecoded> ValidateTokenAsync(string token);
    }
}
