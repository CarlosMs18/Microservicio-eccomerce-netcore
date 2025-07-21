using Prometheus;
using Serilog;
using Shared.Infrastructure.Interfaces;

namespace Catalog.Infrastructure.Services.Internal
{
    public class CatalogMetricsService : IMetricsService
    {
        private readonly Counter _requestCounter;
        private readonly Histogram _requestDuration;
        private readonly Gauge _activeConnections;

        public CatalogMetricsService()
        {
            Log.Information("Inicializando CatalogMetricsService...");

            _requestCounter = Metrics.CreateCounter(
                "catalog_service_requests_total",
                "Total requests to catalog service",
                new[] { "endpoint", "method" });

            _requestDuration = Metrics.CreateHistogram(
                "catalog_service_request_duration_seconds",
                "Request duration in seconds",
                new[] { "endpoint" });

            _activeConnections = Metrics.CreateGauge(
                "catalog_service_active_connections",
                "Active connections to catalog service");

            // Inicializar solo lo necesario
            InitializeMetrics();
            Log.Information("CatalogMetricsService inicializado correctamente");
        }

        private void InitializeMetrics()
        {
            // ✅ Inicializar gauge con valor inicial
            _activeConnections.Set(0);

            // ✅ Pre-registrar solo las combinaciones reales de tus endpoints

            // CategoryController endpoints
            _requestCounter.WithLabels("api/category", "POST");        // CreateCategory
            _requestCounter.WithLabels("api/category/{id}", "GET");    // GetCategory
            _requestCounter.WithLabels("api/category", "GET");         // GetAll

            // ProductController endpoints
            _requestCounter.WithLabels("api/product/updateproductprice", "PUT");  // UpdateProductPrice
            _requestCounter.WithLabels("api/product/getallproducts", "GET");      // GetAllProducts
            _requestCounter.WithLabels("api/product/getproductbyid/{id}", "GET"); // GetProductById

            // ✅ Los histograms se crean automáticamente cuando se usan
            // No necesitan pre-registro

            Log.Information("Métricas de Catalog inicializadas correctamente");
        }

        public void IncrementRequestCount(string endpoint, string method)
        {
            _requestCounter.WithLabels(endpoint, method).Inc();
        }

        public void RecordRequestDuration(string endpoint, double duration)
        {
            Log.Information($"📊 Registrando duración en Catalog: {endpoint} = {duration} segundos");
            _requestDuration.WithLabels(endpoint).Observe(duration);
        }

        public void UpdateActiveConnections(int delta)
        {
            _activeConnections.Inc(delta);
        }
    }
}