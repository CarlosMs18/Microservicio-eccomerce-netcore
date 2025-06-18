using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Core.Interfaces;

namespace User.IntegrationTests.Fixtures
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        public Mock<IExternalAuthService> MockExternalAuthService { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // 🔧 Remover servicios reales
                services.RemoveAll<IExternalAuthService>();

                // 🎭 Configurar mocks
                MockExternalAuthService = new Mock<IExternalAuthService>();
                services.AddSingleton(MockExternalAuthService.Object);

                // 📝 Configurar logging para tests
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            });

            // 🌍 Configurar entorno de testing
            builder.UseEnvironment("Testing");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 🧹 Limpiar recursos
                MockExternalAuthService?.Reset();
            }
            base.Dispose(disposing);
        }
    }
}
