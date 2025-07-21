using Microsoft.Extensions.Hosting;
using Serilog;
using Shared.Infrastructure.Interfaces;

namespace Cart.Infrastructure.Infrastructure
{
    public class MetricsInitializationService : IHostedService
    {
        private readonly IMetricsService _metricsService;

        public MetricsInitializationService(IMetricsService metricsService)
        {
            Log.Information("🚀 MetricsInitializationService: Inicializando...");
            _metricsService = metricsService; // Esto fuerza la creación del UserMetricsService
            Log.Information("✅ MetricsInitializationService: Métricas inicializadas correctamente");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("🟢 MetricsInitializationService: Servicio iniciado");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("🔴 MetricsInitializationService: Servicio detenido");
            return Task.CompletedTask;
        }
    }
}
