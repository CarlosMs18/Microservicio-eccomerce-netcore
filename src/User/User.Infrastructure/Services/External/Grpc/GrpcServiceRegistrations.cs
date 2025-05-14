using Grpc.AspNetCore.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using User.Infrastructure.Services.External.Grpc.Interceptors;
namespace User.Infrastructure.Services.External.Grpc
{
    public static class GrpcServiceRegistrations
    {
        public static IServiceCollection AddGrpcServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Configuración base de gRPC Server
            services.AddGrpc(options =>
            {
                // Opciones recomendadas para producción
                options.EnableDetailedErrors = configuration.GetValue<bool>("Grpc:EnableDetailedErrors", false); // Solo en desarrollo
                options.MaxReceiveMessageSize = configuration.GetValue<int>("Grpc:MaxMessageSizeMB", 4) * 1024 * 1024; // Default: 4MB
                options.IgnoreUnknownServices = true; // Evita errores por servicios no implementados

                // Interceptores (opcionales, pero útiles)
               options.Interceptors.Add<ExceptionInterceptor>(); /// Ejemplo: Manejo centralizado de excepciones
            });

            // 2. Registro del servicio gRPC (Auth)
            services.AddScoped<AuthGrpcService>();

            // 3. Health Checks (para Kubernetes/Liveness probes)
            services.AddGrpcHealthChecks()
                   .AddCheck("auth_service", () =>
                       HealthCheckResult.Healthy("Auth gRPC service is healthy"));

            // 4. Configuración avanzada (opcional)
            ConfigureAspNetCoreGrpc(services, configuration);

            return services;
        }

        private static void ConfigureAspNetCoreGrpc(IServiceCollection services, IConfiguration config)
        {
            // Ejemplo: Configuración específica para ASP.NET Core
            services.Configure<GrpcServiceOptions>(options =>
            {
                options.EnableDetailedErrors = config.GetValue<bool>("Grpc:EnableDetailedErrors");
                options.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            });
        }
    }
}