using Prometheus;
using Shared.Infrastructure.Interfaces;
using Serilog;

namespace User.Infrastructure.Services.Internal
{
    public class UserMetricsService : IMetricsService
    {
        private readonly Counter _requestCounter;
        private readonly Histogram _requestDuration;
        private readonly Gauge _activeConnections;

        public UserMetricsService()
        {
            Log.Information("Inicializando UserMetricsService...");

            _requestCounter = Metrics.CreateCounter(
                "user_service_requests_total",
                "Total requests to user service",
                new[] { "endpoint", "method" });

            _requestDuration = Metrics.CreateHistogram(
                "user_service_request_duration_seconds",
                "Request duration in seconds",
                new[] { "endpoint" });

            _activeConnections = Metrics.CreateGauge(
                "user_service_active_connections",
                "Active connections to user service");

            // Inicializar solo lo necesario
            InitializeMetrics();
            Log.Information("UserMetricsService inicializado correctamente");
        }

        private void InitializeMetrics()
        {
            // ✅ Inicializar gauge con valor inicial
            _activeConnections.Set(0);

            // ✅ OPCIÓN 1: Pre-registrar solo las combinaciones reales
            // Solo registrar las métricas que realmente se usan
            _requestCounter.WithLabels("api/user/login", "POST");
            _requestCounter.WithLabels("api/user/registeruser", "POST");
            _requestCounter.WithLabels("api/user/validate-token", "GET");

            // ✅ Los histograms se crean automáticamente cuando se usan
            // No necesitan pre-registro

            Log.Information("Métricas inicializadas correctamente");
        }

        // ✅ OPCIÓN 2: Eliminar completamente el pre-registro
        // Comentar el método InitializeMetrics y dejar que las métricas
        // se creen automáticamente cuando se usan por primera vez
        /*
        private void InitializeMetrics()
        {
            // Solo inicializar el gauge
            _activeConnections.Set(0);
            Log.Information("Métricas inicializadas correctamente");
        }
        */

        public void IncrementRequestCount(string endpoint, string method)
        {
            _requestCounter.WithLabels(endpoint, method).Inc();
        }

        public void RecordRequestDuration(string endpoint, double duration)
        {
            Log.Information($"📊 Registrando duración: {endpoint} = {duration} segundos");
            _requestDuration.WithLabels(endpoint).Observe(duration);
        }

        public void UpdateActiveConnections(int delta)
        {
            _activeConnections.Inc(delta);
        }
    }
}