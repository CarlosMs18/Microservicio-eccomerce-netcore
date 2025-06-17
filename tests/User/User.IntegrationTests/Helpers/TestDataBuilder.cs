using User.Application.DTOs.Requests;

namespace User.IntegrationTests.Helpers;

public class TestDataBuilder
{
    public static class Registration
    {
        public static RegistrationRequest ValidRequest() => new()
        {
            FirstName = "Juan",
            LastName = "Pérez",
            Email = $"test-{Guid.NewGuid():N}@example.com", // Email único
            Age = 25,
            PhoneNumber = "123456789",
            Address = "Calle Test 123",
            Password = "Test123456!"
        };

        public static RegistrationRequest WithEmail(string email) => new()
        {
            FirstName = "Juan",
            LastName = "Pérez",
            Email = email,
            Age = 25,
            PhoneNumber = "123456789",
            Address = "Calle Test 123",
            Password = "Test123456!"
        };

        public static RegistrationRequest WithPassword(string password) => new()
        {
            FirstName = "Juan",
            LastName = "Pérez",
            Email = $"test-{Guid.NewGuid():N}@example.com",
            Age = 25,
            PhoneNumber = "123456789",
            Address = "Calle Test 123",
            Password = password
        };

        public static RegistrationRequest EmptyFirstName() => new()
        {
            FirstName = "",
            LastName = "Pérez",
            Email = $"test-{Guid.NewGuid():N}@example.com",
            Age = 25,
            PhoneNumber = "123456789",
            Address = "Calle Test 123",
            Password = "Test123456!"
        };

        public static RegistrationRequest InvalidEmail() => new()
        {
            FirstName = "Juan",
            LastName = "Pérez",
            Email = "invalid-email",
            Age = 25,
            PhoneNumber = "123456789",
            Address = "Calle Test 123",
            Password = "Test123456!"
        };
    }

    public static class Login
    {
        public static LoginRequest ValidRequest(string email, string password) => new()
        {
            Email = email,
            Password = password
        };

        public static LoginRequest WithCredentials(string email, string password) => new()
        {
            Email = email,
            Password = password
        };
    }
}