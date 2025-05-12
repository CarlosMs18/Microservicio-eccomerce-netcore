using Microsoft.AspNetCore.Http;
using Shared.Core.Constants;
using System.Security.Claims;

namespace Shared.Core.Handlers
{
    public abstract class BaseHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        protected BaseHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Propiedades reutilizables
        protected string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirst(CustomClaimTypes.UID)?.Value;
        protected string? UserEmail => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    }
}
