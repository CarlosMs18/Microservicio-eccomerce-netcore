using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using User.Application.DTOs.Requests;
using User.Application.DTOs.Responses;
using User.Application.Models;
using User.IntegrationTests.Builders;
using User.IntegrationTests.Common;
using User.IntegrationTests.Fixtures;
using Xunit;

namespace User.IntegrationTests.Controllers
{
    [Collection("Sequential")] // Evitar conflictos entre tests
    public class UserControllerLoginTests : BaseIntegrationTest
    {
        private const string LoginEndpoint = "/api/user/login";
        private const string TestUserEmail = "logintest@test.com";
        private const string TestUserPassword = "Password123!";

        public UserControllerLoginTests(CustomWebApplicationFactory<Program> factory)
            : base(factory) { }

        #region Success Tests

        [Fact]
        public async Task Login_ValidCredentials_ShouldReturnSuccessResponse()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            // Create test user
            await Factory.CreateTestUserAsync(TestUserEmail, TestUserPassword);

            var request = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword(TestUserPassword)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(content, JsonOptions);

            result.Should().NotBeNull();
            result.Token.Should().NotBeNullOrEmpty();
            result.UserId.Should().NotBeNullOrEmpty();
            result.Email.Should().Be(TestUserEmail);
            result.UserName.Should().Be(TestUserEmail); // Por defecto UserName = Email
        }

        [Fact]
        public async Task Login_ExistingUserWithDifferentCasing_ShouldReturnSuccess()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var userEmail = "CaseTest@Example.COM";
            await Factory.CreateTestUserAsync(userEmail, TestUserPassword);

            var request = LoginRequestBuilder.ValidUser()
                .WithEmail(userEmail.ToLower()) // Probar con minúsculas
                .WithPassword(TestUserPassword)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Error Tests - Authentication

        [Fact]
        public async Task Login_NonExistentUser_ShouldReturnUnauthorized()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request = LoginRequestBuilder.NonExistentUser()
                .WithPassword(TestUserPassword)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_WrongPassword_ShouldReturnUnauthorized()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            await Factory.CreateTestUserAsync(TestUserEmail, TestUserPassword);

            var request = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword("WrongPassword123!")
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Error Tests - Validation

        [Theory]
        [InlineData("", "Password123!")] // Email vacío
        [InlineData("invalid-email", "Password123!")] // Email inválido
        [InlineData("test@test.com", "")] // Password vacío
        [InlineData(null, "Password123!")] // Email null
        [InlineData("test@test.com", null)] // Password null
        public async Task Login_InvalidData_ShouldReturnBadRequest(string email, string password)
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_EmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var jsonContent = new StringContent("{}", Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_NullRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var jsonContent = new StringContent("null", Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Error Tests - Content Type

        [Fact]
        public async Task Login_InvalidContentType_ShouldReturnUnsupportedMediaType()
        {
            // Arrange
            var request = LoginRequestBuilder.ValidUser().Build();
            var xmlContent = new StringContent(
                "<root>test</root>",
                Encoding.UTF8,
                "application/xml");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, xmlContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        }

        [Fact]
        public async Task Login_MalformedJson_ShouldReturnBadRequest()
        {
            // Arrange
            var malformedJson = new StringContent(
                "{ invalid json }",
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, malformedJson);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Token Validation Tests

        [Fact]
        public async Task Login_Success_ShouldReturnValidJwtToken()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            await Factory.CreateTestUserAsync(TestUserEmail, TestUserPassword);

            var request = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword(TestUserPassword)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(content, JsonOptions);

            // Validate JWT token structure (basic validation)
            result.Token.Should().NotBeNullOrEmpty();
            result.Token.Split('.').Should().HaveCount(3); // JWT has 3 parts separated by dots
        }

        #endregion

        #region Security Tests

        [Fact]
        public async Task Login_MultipleFailedAttempts_ShouldStillValidateEachIndependently()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            await Factory.CreateTestUserAsync(TestUserEmail, TestUserPassword);

            var wrongRequest = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword("WrongPassword!")
                .Build();

            var correctRequest = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword(TestUserPassword)
                .Build();

            var wrongJsonContent = new StringContent(
                JsonSerializer.Serialize(wrongRequest, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var correctJsonContent = new StringContent(
                JsonSerializer.Serialize(correctRequest, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act - Multiple failed attempts
            var failedResponse1 = await Client.PostAsync(LoginEndpoint, wrongJsonContent);
            var failedResponse2 = await Client.PostAsync(LoginEndpoint,
                new StringContent(JsonSerializer.Serialize(wrongRequest, JsonOptions), Encoding.UTF8, "application/json"));

            // Act - Correct attempt should still work
            var successResponse = await Client.PostAsync(LoginEndpoint, correctJsonContent);

            // Assert
            failedResponse1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            failedResponse2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            successResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Database State Tests

        [Fact]
        public async Task Login_Success_ShouldNotModifyUserData()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            await Factory.CreateTestUserAsync(TestUserEmail, TestUserPassword);

            // Get initial user state
            using var initialScope = Factory.Services.CreateScope();
            var initialUserManager = initialScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var initialUser = await initialUserManager.FindByEmailAsync(TestUserEmail);

            var request = LoginRequestBuilder.ValidUser()
                .WithEmail(TestUserEmail)
                .WithPassword(TestUserPassword)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(LoginEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify user data wasn't modified
            using var finalScope = Factory.Services.CreateScope();
            var finalUserManager = finalScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var finalUser = await finalUserManager.FindByEmailAsync(TestUserEmail);

            finalUser.Should().NotBeNull();
            finalUser.Email.Should().Be(initialUser.Email);
            finalUser.FirstName.Should().Be(initialUser.FirstName);
            finalUser.LastName.Should().Be(initialUser.LastName);
            finalUser.EmailConfirmed.Should().Be(initialUser.EmailConfirmed);
        }

        #endregion
    }
}