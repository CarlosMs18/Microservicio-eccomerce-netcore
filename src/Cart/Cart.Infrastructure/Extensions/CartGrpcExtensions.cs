using Cart.Application.Contracts.External;
using Cart.Infrastructure.SyncDataServices.Grpc;
using Catalog.Grpc;
using User.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Extensions;

namespace Cart.Infrastructure.Extensions
{
    public static class CartGrpcExtensions
    {
        public static IServiceCollection AddCartGrpcClients(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var grpcTemplate = configuration["Microservices:User:GrpcTemplate"] ?? "http://{host}:{port}";
            Console.WriteLine("OYE!");
            Console.WriteLine(grpcTemplate);
            Console.WriteLine(configuration["Microservices:User:host"]);
            Console.WriteLine(configuration["Microservices:User:port"]);
            Console.Write(grpcTemplate);
            // ✅ User Service - Lee desde Microservices:User
            var userHost = configuration["Microservices:User:host"] ?? "localhost";
            var userPort = configuration["Microservices:User:port"] ?? "5003";
            var userUrl = GrpcClientExtensions.BuildGrpcUrl(grpcTemplate, userHost, userPort);

            services.AddGrpcClientWithResilience<AuthService.AuthServiceClient>(userUrl)
                    .AddSingleton<IUserGrpcClient, UserGrpcClient>();

            // ✅ Catalog Service - Lee desde Microservices:Catalog  
            var catalogHost = configuration["Microservices:Catalog:host"] ?? "localhost";
            var catalogPort = configuration["Microservices:Catalog:port"] ?? "7204";
            var catalogUrl = GrpcClientExtensions.BuildGrpcUrl(grpcTemplate, catalogHost, catalogPort);

            services.AddGrpcClientWithResilience<CatalogService.CatalogServiceClient>(catalogUrl)
                    .AddSingleton<ICatalogService, CatalogGrpcService>();

            return services;
        }
    }
}