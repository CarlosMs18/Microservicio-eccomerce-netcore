
namespace Shared.Infrastructure.Interfaces
{
    public interface IMetricsService
    {
        void IncrementRequestCount(string endpoint, string method);
        void RecordRequestDuration(string endpoint, double duration);
        void UpdateActiveConnections(int delta);
    }
}
