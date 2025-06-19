using User.Application.DTOs.Requests;

namespace User.IntegrationTests.Builders
{
    public class LoginRequestBuilder
    {
        private readonly LoginRequest _request;

        public LoginRequestBuilder()
        {
            _request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123!"
            };
        }

        public LoginRequestBuilder WithEmail(string email)
        {
            _request.Email = email;
            return this;
        }

        public LoginRequestBuilder WithPassword(string password)
        {
            _request.Password = password;
            return this;
        }

        public LoginRequest Build() => _request;

        // Métodos de conveniencia para casos comunes
        public static LoginRequestBuilder ValidUser() => new();

        public static LoginRequestBuilder InvalidEmail() => new LoginRequestBuilder()
            .WithEmail("invalid-email");

        public static LoginRequestBuilder EmptyPassword() => new LoginRequestBuilder()
            .WithPassword("");

        public static LoginRequestBuilder WeakPassword() => new LoginRequestBuilder()
            .WithPassword("123");

        public static LoginRequestBuilder NonExistentUser() => new LoginRequestBuilder()
            .WithEmail("nonexistent@test.com");
    }
}