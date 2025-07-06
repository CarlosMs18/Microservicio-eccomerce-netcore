using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Application.Constants;

namespace User.UnitTests.Infrastructure.Services
{
    public class ExternalAuthServiceTests
    {
        private readonly ExternalAuthService _authService;
        private readonly JwtSettings _jwtSettings;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public ExternalAuthServiceTests()
        {
            // Configuración JWT para las pruebas
            _jwtSettings = new JwtSettings
            {
                Key = "ThisIsASecretKeyForTestingPurposesOnly123456789",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                DurationInMinutes = 60
            };

            var options = Options.Create(_jwtSettings);
            _authService = new ExternalAuthService(options);
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        [Fact]
        public async Task ValidateTokenAsync_WithValidToken_ShouldReturnValidResult()
        {
            // Arrange
            var userId = "12345";
            var email = "test@example.com";
            var roles = new[] { "User", "Admin" };
            var token = GenerateValidToken(userId, email, roles);

            // Act
            var result = await _authService.ValidateTokenAsync(token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(email, result.Email);
            Assert.Equal(2, result.Roles.Count);
            Assert.Contains("User", result.Roles);
            Assert.Contains("Admin", result.Roles);
            Assert.NotEmpty(result.Claims);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithExpiredToken_ShouldReturnInvalidResult()
        {
            // Arrange
            var expiredToken = GenerateExpiredToken();

            // Act
            var result = await _authService.ValidateTokenAsync(expiredToken);

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.UserId);
            Assert.Null(result.Email);
            Assert.Empty(result.Roles);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithInvalidSignature_ShouldReturnInvalidResult()
        {
            // Arrange
            var tokenWithInvalidSignature = GenerateTokenWithInvalidSignature();

            // Act
            var result = await _authService.ValidateTokenAsync(tokenWithInvalidSignature);

            // Assert
            Assert.False(result.IsValid);
            Assert.Null(result.UserId);
            Assert.Null(result.Email);
            Assert.Empty(result.Roles);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithWrongIssuer_ShouldReturnInvalidResult()
        {
            // Arrange
            var tokenWithWrongIssuer = GenerateTokenWithWrongIssuer();

            // Act
            var result = await _authService.ValidateTokenAsync(tokenWithWrongIssuer);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithWrongAudience_ShouldReturnInvalidResult()
        {
            // Arrange
            var tokenWithWrongAudience = GenerateTokenWithWrongAudience();

            // Act
            var result = await _authService.ValidateTokenAsync(tokenWithWrongAudience);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithMalformedToken_ShouldReturnInvalidResult()
        {
            // Arrange
            var malformedToken = "this.is.not.a.valid.jwt.token";

            // Act
            var result = await _authService.ValidateTokenAsync(malformedToken);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithEmptyToken_ShouldReturnInvalidResult()
        {
            // Arrange
            var emptyToken = string.Empty;

            // Act
            var result = await _authService.ValidateTokenAsync(emptyToken);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithNullToken_ShouldReturnInvalidResult()
        {
            // Arrange
            string nullToken = null;

            // Act
            var result = await _authService.ValidateTokenAsync(nullToken);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithTokenWithoutRoles_ShouldReturnValidResultWithEmptyRoles()
        {
            // Arrange
            var userId = "12345";
            var email = "test@example.com";
            var token = GenerateTokenWithoutRoles(userId, email);

            // Act
            var result = await _authService.ValidateTokenAsync(token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(email, result.Email);
            Assert.Empty(result.Roles);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithTokenWithoutUserId_ShouldReturnValidResultWithNullUserId()
        {
            // Arrange
            var email = "test@example.com";
            var roles = new[] { "User" };
            var token = GenerateTokenWithoutUserId(email, roles);

            // Act
            var result = await _authService.ValidateTokenAsync(token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.UserId);
            Assert.Equal(email, result.Email);
            Assert.Single(result.Roles);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithValidToken_ShouldPopulateClaimsDictionary()
        {
            // Arrange
            var userId = "12345";
            var email = "test@example.com";
            var roles = new[] { "User" };
            var token = GenerateValidToken(userId, email, roles);

            // Act
            var result = await _authService.ValidateTokenAsync(token);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Claims);
            Assert.True(result.Claims.ContainsKey(CustomClaimTypes.UID));
            Assert.True(result.Claims.ContainsKey(ClaimTypes.Email));
            Assert.True(result.Claims.ContainsKey(ClaimTypes.Role));
            Assert.Equal(userId, result.Claims[CustomClaimTypes.UID]);
            Assert.Equal(email, result.Claims[ClaimTypes.Email]);
        }

        #region Helper Methods

        private string GenerateValidToken(string userId, string email, string[] roles)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            // Agregar roles
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateExpiredToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, "12345"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(-10), // Token expirado hace 10 minutos
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateTokenWithInvalidSignature()
        {
            // Generar token con una clave diferente
            var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("WrongSecretKeyForTesting123456789"));
            var creds = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, "12345"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateTokenWithWrongIssuer()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, "12345"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };

            var token = new JwtSecurityToken(
                issuer: "WrongIssuer", // Issuer incorrecto
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateTokenWithWrongAudience()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, "12345"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: "WrongAudience", // Audience incorrecto
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateTokenWithoutRoles(string userId, string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.UID, userId),
                new Claim(ClaimTypes.Email, email)
                // Sin roles
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateTokenWithoutUserId(string email, string[] roles)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email)
                // Sin UserId
            };

            // Agregar roles
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: creds
            );

            return _tokenHandler.WriteToken(token);
        }

        #endregion
    }
}