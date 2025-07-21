using Prometheus;
using Serilog;
using Shared.Infrastructure.Interfaces;


namespace Cart.Infrastructure.Services.Internal
{
    public class CartMetricsService : IMetricsService
    {
        private readonly Counter _requestCounter;
        private readonly Histogram _requestDuration;
        private readonly Gauge _activeConnections;

        public CartMetricsService()
        {
            Log.Information("Inicializando CartMetricsService...");

            _requestCounter = Metrics.CreateCounter(
                "cart_service_requests_total",
                "Total requests to cart service",
                new[] { "endpoint", "method" });

            _requestDuration = Metrics.CreateHistogram(
                "cart_service_request_duration_seconds",
                "Request duration in seconds",
                new[] { "endpoint" });

            _activeConnections = Metrics.CreateGauge(
                "cart_service_active_connections",
                "Active connections to cart service");

            // Inicializar solo lo necesario
            InitializeMetrics();
            Log.Information("CartMetricsService inicializado correctamente");
        }

        private void InitializeMetrics()
        {
            // ✅ Inicializar gauge con valor inicial
            _activeConnections.Set(0);

            // ✅ Pre-registrar solo las combinaciones reales de tus endpoints
            // CartController endpoints
            _requestCounter.WithLabels("api/cart/addproducttocart", "POST");  // AddProductToCart

            // ✅ Los histograms se crean automáticamente cuando se usan
            // No necesitan pre-registro
            Log.Information("Métricas de Cart inicializadas correctamente");
        }

        public void IncrementRequestCount(string endpoint, string method)
        {
            _requestCounter.WithLabels(endpoint, method).Inc();
        }

        public void RecordRequestDuration(string endpoint, double duration)
        {
            Log.Information($"📊 Registrando duración en Cart: {endpoint} = {duration} segundos");
            _requestDuration.WithLabels(endpoint).Observe(duration);
        }

        public void UpdateActiveConnections(int delta)
        {
            _activeConnections.Inc(delta);
        }
    }
}