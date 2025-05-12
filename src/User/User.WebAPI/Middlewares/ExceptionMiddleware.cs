using Newtonsoft.Json;
using System.Net;
using User.Application.Exceptions;
using User.WebAPI.Errors;

namespace User.WebAPI.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                var statusCode = ex is NotFoundException ? HttpStatusCode.NotFound :
                                 ex is ValidationException || ex is BadRequestException ? HttpStatusCode.BadRequest :
                                 HttpStatusCode.InternalServerError;
                IEnumerable<KeyValuePair<string, string>>? messageList = ex is ValidationException ve ? ve.Errors : null;

                var result = JsonConvert.SerializeObject(new CodeErrorResponse((int)statusCode, ex.Message, ex.StackTrace, messageList));

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)statusCode;

                await context.Response.WriteAsync(result);
            }
        }
    }
}
