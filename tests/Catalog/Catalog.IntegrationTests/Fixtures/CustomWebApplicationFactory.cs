using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Catalog.Tests.Integration
{
    public class CustomApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // IMPORTANTE: Configurar el entorno ANTES que todo
            builder.UseEnvironment("Testing");

            // También configurar la variable de entorno por si acaso
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Asegurar que el contexto reconozca el entorno Testing
                context.HostingEnvironment.EnvironmentName = "Testing";

                // Limpiar configuraciones previas y cargar las correctas
                config.Sources.Clear();
                config.AddJsonFile("appsettings.json", optional: false)
                      .AddJsonFile("appsettings.Testing.json", optional: true)
                      .AddEnvironmentVariables();
            });

            builder.ConfigureServices(services =>
            {
                // Configurar logging para pruebas (menos verbose)
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });

                // Opcional: Si necesitas override de servicios específicos para testing
                // Por ejemplo, si quieres usar una base de datos en memoria diferente
                // services.RemoveAll<DbContext>();
                // services.AddDbContext<YourContext>(options => options.UseInMemoryDatabase("TestDB"));
            });
        }
    }
}