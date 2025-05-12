namespace User.Application.DTOs.Responses
{
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
    }
}
