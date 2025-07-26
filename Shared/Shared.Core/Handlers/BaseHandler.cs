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

            var environment = DetectEnvironment();
            Console.WriteLine($"Entorno detectado en BaseHandler: {environment}");

            switch (environment)
            {
                case "CI":
                    // ✅ CI: Leer desde Claims (TestingAuthHandler los pone ahí)
                    var ciUserId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? context.User?.FindFirst("user_id")?.Value;
                    Console.WriteLine($"CI UserId desde Claims: {ciUserId}");
                    return ciUserId;

                case "Testing":
                    // ✅ TESTING: Leer desde Claims (TestingAuthHandler los pone ahí)
                    var testUserId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? context.User?.FindFirst("user_id")?.Value;
                    Console.WriteLine($"Testing UserId desde Claims: {testUserId}");
                    return testUserId;

                case "Production":
                case "Kubernetes":
                    // ✅ PRODUCTION/KUBERNETES: Leer desde headers del Ingress
                    var ingressUserId = context.Request.Headers["x-user-id"].ToString();
                    Console.WriteLine($"{environment} UserId desde Headers: {ingressUserId}");
                    return ingressUserId;

                default: // Development/Docker
                    // ✅ DEVELOPMENT/DOCKER: Leer desde Claims (middleware)
                    var devUserId = context.User?.FindFirst(CustomClaimTypes.UID)?.Value;
                    Console.WriteLine($"Development UserId desde Claims: {devUserId}");
                    return devUserId;
            }
        }

        private string? GetUserEmail()
        {
            Console.WriteLine("GETUSEREMAIL BASE HANDLER");

            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var environment = DetectEnvironment();

            switch (environment)
            {
                case "CI":
                    // ✅ CI: Leer desde Claims
                    var ciEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value;
                    Console.WriteLine($"CI Email desde Claims: {ciEmail}");
                    return ciEmail;

                case "Testing":
                    // ✅ TESTING: Leer desde Claims
                    var testEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value;
                    Console.WriteLine($"Testing Email desde Claims: {testEmail}");
                    return testEmail;

                case "Production":
                case "Kubernetes":
                    // ✅ PRODUCTION/KUBERNETES: Leer desde headers del Ingress
                    var ingressEmail = context.Request.Headers["x-user-email"].ToString();
                    Console.WriteLine($"{environment} Email desde Headers: {ingressEmail}");
                    return ingressEmail;

                default: // Development/Docker
                    // ✅ DEVELOPMENT/DOCKER: Leer desde Claims
                    var devEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value;
                    Console.WriteLine($"Development Email desde Claims: {devEmail}");
                    return devEmail;
            }
        }

        private List<string> GetUserRoles()
        {
            Console.WriteLine("GETUSERROLES BASE HANDLER");

            var context = _httpContextAccessor.HttpContext;
            if (context == null) return new List<string>();

            var environment = DetectEnvironment();

            switch (environment)
            {
                case "CI":
                    // ✅ CI: Leer desde Claims
                    var ciRoles = context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();
                    Console.WriteLine($"CI Roles desde Claims: {string.Join(",", ciRoles)}");
                    return ciRoles;

                case "Testing":
                    // ✅ TESTING: Leer desde Claims
                    var testRoles = context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();
                    Console.WriteLine($"Testing Roles desde Claims: {string.Join(",", testRoles)}");
                    return testRoles;

                case "Production":
                case "Kubernetes":
                    // ✅ PRODUCTION/KUBERNETES: Leer desde headers del Ingress
                    var rolesHeader = context.Request.Headers["x-user-roles"].ToString();
                    var ingressRoles = string.IsNullOrEmpty(rolesHeader)
                        ? new List<string>()
                        : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(r => r.Trim())
                                     .ToList();
                    Console.WriteLine($"{environment} Roles desde Headers: {string.Join(",", ingressRoles)}");
                    return ingressRoles;

                default: // Development/Docker
                    // ✅ DEVELOPMENT/DOCKER: Leer desde Claims
                    var devRoles = context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();
                    Console.WriteLine($"Development Roles desde Claims: {string.Join(",", devRoles)}");
                    return devRoles;
            }
        }

        // Método helper para detectar entorno
        private string DetectEnvironment()
        {
            // 🔥 PRIORIDAD: ASPNETCORE_ENVIRONMENT tiene la máxima prioridad
            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(aspnetEnv))
            {
                return aspnetEnv;
            }

            // Fallbacks para otros entornos
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                return "CI";
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
                return "Kubernetes";
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
                return "Docker";

            return "Development";
        }
    }
}