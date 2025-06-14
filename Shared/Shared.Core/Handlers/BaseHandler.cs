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

        // Propiedades reutilizables que funcionan en todos los entornos
        protected string? UserId => GetUserId();
        protected string? UserEmail => GetUserEmail();
        protected List<string> UserRoles => GetUserRoles();

        private string? GetUserId()
        {
            Console.WriteLine("GETUSERID BASE HANDLER");
         
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;
            Console.WriteLine($"{context.Request.Headers["x-user-id"].ToString()}");
            Console.WriteLine("Ojito");
            // ✅ KUBERNETES: Leer desde headers del Ingress
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            {
                return context.Request.Headers["x-user-id"].ToString();
            }

            // ✅ DEVELOPMENT/DOCKER: Leer desde Claims (middleware)
            return context.User?.FindFirst(CustomClaimTypes.UID)?.Value;
        }

        private string? GetUserEmail()
        {
            Console.WriteLine("GETUSEREMAIL BASE HANDLER");
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // ✅ KUBERNETES: Leer desde headers del Ingress
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            {
                return context.Request.Headers["x-user-email"].ToString();
            }

            // ✅ DEVELOPMENT/DOCKER: Leer desde Claims (middleware)
            return context.User?.FindFirst(ClaimTypes.Email)?.Value;
        }

        private List<string> GetUserRoles()
        {
            Console.WriteLine("GETUSERROLES BASE HANDLER");
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return new List<string>();

            // ✅ KUBERNETES: Leer desde headers del Ingress
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            {
                var rolesHeader = context.Request.Headers["x-user-roles"].ToString();
                return string.IsNullOrEmpty(rolesHeader)
                    ? new List<string>()
                    : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(r => r.Trim())
                                 .ToList();
            }

            // ✅ DEVELOPMENT/DOCKER: Leer desde Claims (middleware)
            return context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList() ?? new List<string>();
        }
    }
}