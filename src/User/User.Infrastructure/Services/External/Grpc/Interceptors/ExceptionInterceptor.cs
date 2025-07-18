using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog;
using System;

namespace User.Infrastructure.Services.External.Grpc.Interceptors
{
    public class ExceptionInterceptor : Interceptor
    {
        private readonly ILogger<ExceptionInterceptor> _logger;

        public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
        {
            Log.Information("🔧 ExceptionInterceptor: Iniciando constructor...");
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                Console.WriteLine("Oyeeee!");
                return await continuation(request, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en gRPC: {context.Method}");
                throw new RpcException(new Status(StatusCode.Internal, "Error interno del servidor"));
            }
        }
    }
}