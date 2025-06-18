using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Core.Interfaces;
using System.Net.Http;
using System.Text.Json;
using User.IntegrationTests.Fixtures;
using Xunit;

namespace User.IntegrationTests.Common
{
    public abstract class BaseIntegrationTest : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
    {
        protected readonly CustomWebApplicationFactory<Program> Factory;
        protected readonly HttpClient Client;
        protected readonly Mock<IExternalAuthService> MockExternalAuthService;

        // 🎯 Opciones de serialización reutilizables
        protected readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        protected BaseIntegrationTest(CustomWebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = factory.CreateClient();
            MockExternalAuthService = factory.MockExternalAuthService;
        }

        // 🧹 Limpiar después de cada test
        protected virtual void ResetMocks()
        {
            MockExternalAuthService.Reset();
        }

        // 🌍 Helpers para configurar entorno
        protected void SetKubernetesEnvironment(bool isKubernetes = true)
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST",
                isKubernetes ? "kubernetes.default.svc" : null);
        }

        public virtual void Dispose()
        {
            ResetMocks();
            SetKubernetesEnvironment(false); // Reset environment
            GC.SuppressFinalize(this);
        }
    }
}