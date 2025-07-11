﻿using Microsoft.AspNetCore.Http;
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

                case "Kubernetes":
                    // ✅ KUBERNETES: Leer desde headers del Ingress
                    var kubeUserId = context.Request.Headers["x-user-id"].ToString();
                    Console.WriteLine($"Kubernetes UserId desde Headers: {kubeUserId}");
                    return kubeUserId;

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
                    return context.User?.FindFirst(ClaimTypes.Email)?.Value;

                case "Testing":
                    // ✅ TESTING: Leer desde Claims
                    return context.User?.FindFirst(ClaimTypes.Email)?.Value;

                case "Kubernetes":
                    // ✅ KUBERNETES: Leer desde headers del Ingress
                    return context.Request.Headers["x-user-email"].ToString();

                default: // Development/Docker
                    // ✅ DEVELOPMENT/DOCKER: Leer desde Claims
                    return context.User?.FindFirst(ClaimTypes.Email)?.Value;
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
                    return context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();

                case "Testing":
                    // ✅ TESTING: Leer desde Claims
                    return context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();

                case "Kubernetes":
                    // ✅ KUBERNETES: Leer desde headers del Ingress
                    var rolesHeader = context.Request.Headers["x-user-roles"].ToString();
                    return string.IsNullOrEmpty(rolesHeader)
                        ? new List<string>()
                        : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(r => r.Trim())
                                     .ToList();

                default: // Development/Docker
                    // ✅ DEVELOPMENT/DOCKER: Leer desde Claims
                    return context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList()
                           ?? new List<string>();
            }
        }

        // Método helper para detectar entorno (actualizado para incluir CI)
        private string DetectEnvironment()
        {
            // 🆕 PRIORIDAD 1: Detectar CI primero
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                return "CI";

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Testing") return "Testing";

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
                return "Kubernetes";

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
                return "Docker";

            return "Development";
        }
    }
}