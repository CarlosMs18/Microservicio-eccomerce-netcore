namespace Cart.WebAPI.Errors
{
    public class CodeErrorResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
        public IEnumerable<KeyValuePair<string, string>>? MessageList { get; set; }

        public CodeErrorResponse(int statusCode, string? message = null, string? stackTrace = null, IEnumerable<KeyValuePair<string, string>>? messageList = null)
        {
            Success = false;
            StatusCode = statusCode;
            Message = message ?? GetDefaultMessageStatusCode(statusCode);
            StackTrace = stackTrace;
            MessageList = messageList;
        }

        private string GetDefaultMessageStatusCode(int statusCode)
        {
            return statusCode switch
            {
                400 => "El Request enviado tiene errores",
                401 => "No tienes authorizacion para este recurso",
                404 => "No se encontro el recurso solicitado",
                500 => "Se producieron errores en el servidor",
                _ => string.Empty
            };
        }
    }
}
