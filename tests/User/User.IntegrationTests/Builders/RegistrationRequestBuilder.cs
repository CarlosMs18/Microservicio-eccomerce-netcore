using User.Application.DTOs.Requests;

namespace User.IntegrationTests.Builders
{
    public class RegistrationRequestBuilder
    {
        private readonly RegistrationRequest _request;

        public RegistrationRequestBuilder()
        {
            _request = new RegistrationRequest
            {
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Age = 25,                    // ✅ AGREGADO
                PhoneNumber = "+51987654321",
                Address = "Av. Lima 123",    // ✅ AGREGADO
                Password = "Password123!"
            };
        }

        public RegistrationRequestBuilder WithEmail(string email)
        {
            _request.Email = email;
            return this;
        }

        public RegistrationRequestBuilder WithFirstName(string firstName)
        {
            _request.FirstName = firstName;
            return this;
        }

        public RegistrationRequestBuilder WithLastName(string lastName)
        {
            _request.LastName = lastName;
            return this;
        }

        public RegistrationRequestBuilder WithAge(int age)         // ✅ NUEVO
        {
            _request.Age = age;
            return this;
        }

        public RegistrationRequestBuilder WithPhoneNumber(string phoneNumber)
        {
            _request.PhoneNumber = phoneNumber;
            return this;
        }

        public RegistrationRequestBuilder WithAddress(string address)  // ✅ NUEVO
        {
            _request.Address = address;
            return this;
        }

        public RegistrationRequestBuilder WithPassword(string password)
        {
            _request.Password = password;
            return this;
        }

        public RegistrationRequest Build() => _request;

        // 🏭 Factory methods actualizados
        public static RegistrationRequestBuilder ValidUser()
            => new RegistrationRequestBuilder();

        public static RegistrationRequestBuilder UserWithPhone()
            => new RegistrationRequestBuilder()
                .WithPhoneNumber("+51987654321");

        public static RegistrationRequestBuilder AdminUser()
            => new RegistrationRequestBuilder()
                .WithEmail("admin@company.com")
                .WithFirstName("Admin")
                .WithLastName("User")
                .WithAge(30);

        public static RegistrationRequestBuilder WeakPassword()
            => new RegistrationRequestBuilder()
                .WithPassword("123");

        public static RegistrationRequestBuilder InvalidEmail()
            => new RegistrationRequestBuilder()
                .WithEmail("invalid-email-format");

        public static RegistrationRequestBuilder MinorUser()           // ✅ NUEVO
            => new RegistrationRequestBuilder()
                .WithAge(16);

        public static RegistrationRequestBuilder ElderUser()           // ✅ NUEVO
            => new RegistrationRequestBuilder()
                .WithAge(65);
    }
}