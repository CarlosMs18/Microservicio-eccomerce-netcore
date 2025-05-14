using Shared.Core.Interfaces;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Services.Internal
{
    // En User.Infrastructure
    public class HealthChecker : IHealthChecker
    {
        private readonly UserIdentityDbContext _dbContext;
        private readonly IExternalAuthService _authService;

        public HealthChecker(
            UserIdentityDbContext dbContext,
            IExternalAuthService authService)
        {
            _dbContext = dbContext;
            _authService = authService;
        }

        public async Task<bool> CheckDatabaseHealthAsync()
        {
            return await _dbContext.Database.CanConnectAsync();
        }

        public async Task<bool> CheckExternalDependenciesAsync()
        {
            // Implementa verificaciones reales aquí
            return true;
        }
    }
}
