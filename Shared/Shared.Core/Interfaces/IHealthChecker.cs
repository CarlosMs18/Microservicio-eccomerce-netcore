namespace Shared.Core.Interfaces
{
    public interface IHealthChecker
    {
        Task<bool> CheckDatabaseHealthAsync();
        Task<bool> CheckExternalDependenciesAsync();
    }
}
