using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Catalog.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Forzar el ambiente Testing
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Configurar servicios específicos para testing
        builder.ConfigureServices(services =>
        {
            // Configurar logging más silencioso para tests
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();   
                logging.SetMinimumLevel(LogLevel.Warning); // Solo warnings y errores
            });

            // Aquí puedes override servicios específicos para testing si es necesario
            // Por ejemplo, mockear servicios externos, cambiar base de datos, etc.

            // El TestingAuthHandler ya está configurado automáticamente
            // por el environment "Testing" en tu Program.cs
        });
    }
}