using Shared.Core.Dtos;

namespace User.IntegrationTests.Builders
{
    public class TokenValidationResultBuilder
    {
        private readonly TokenValidationDecoded _result;

        public TokenValidationResultBuilder()
        {
            _result = new TokenValidationDecoded
            {
                IsValid = true,
                UserId = "default-user-id",
                Email = "test@example.com",
                Roles = new List<string> { "User" },
                Claims = new Dictionary<string, string>()
            };
        }

        public TokenValidationResultBuilder WithUserId(string userId)
        {
            _result.UserId = userId;
            return this;
        }

        public TokenValidationResultBuilder WithEmail(string email)
        {
            _result.Email = email;
            return this;
        }

        public TokenValidationResultBuilder WithRoles(params string[] roles)
        {
            _result.Roles = new List<string>(roles);
            return this;
        }

        public TokenValidationResultBuilder WithClaims(Dictionary<string, string> claims)
        {
            _result.Claims = claims;
            return this;
        }

        public TokenValidationResultBuilder AsInvalid()
        {
            _result.IsValid = false;
            return this;
        }

        public TokenValidationResultBuilder WithAdminRole()
        {
            _result.Roles = new List<string> { "Admin", "User" };
            return this;
        }

        public TokenValidationDecoded Build() => _result;

        // 🏭 Factory methods para casos comunes
        public static TokenValidationDecoded ValidAdminToken()
            => new TokenValidationResultBuilder()
                .WithUserId("admin-123")
                .WithEmail("admin@company.com")
                .WithAdminRole()
                .Build();

        public static TokenValidationDecoded ValidUserToken()
            => new TokenValidationResultBuilder()
                .WithUserId("user-456")
                .WithEmail("user@company.com")
                .Build();

        public static TokenValidationDecoded InvalidToken()
            => new TokenValidationResultBuilder()
                .AsInvalid()
                .Build();
    }
}