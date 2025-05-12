namespace User.Application.DTOs.Responses
{
    public class RegistrationResponse
    {
        public bool IsSuccess { get; set; }
        public string UserId { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
    }
}
