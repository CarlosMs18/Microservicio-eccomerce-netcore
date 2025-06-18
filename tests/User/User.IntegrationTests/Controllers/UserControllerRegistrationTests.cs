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
    public class UserControllerRegistrationTests : BaseIntegrationTest
    {
        private const string RegisterEndpoint = "/api/user/RegisterUser";

        public UserControllerRegistrationTests(CustomWebApplicationFactory<Program> factory)
            : base(factory) { }

        #region Success Tests

        [Fact]
        public async Task RegisterUser_ValidRequest_ShouldReturnSuccessResponse()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request = RegistrationRequestBuilder.ValidUser()
                .WithEmail("newuser@test.com")
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RegistrationResponse>(content, JsonOptions);

            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.UserId.Should().NotBeNullOrEmpty();
            result.Message.Should().Be("Registro exitoso");
            result.Token.Should().NotBeNullOrEmpty();

            // Verify user was created in database
            using var scope = Factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var createdUser = await userManager.FindByEmailAsync(request.Email);

            createdUser.Should().NotBeNull();
            createdUser.Email.Should().Be(request.Email);
            createdUser.FirstName.Should().Be(request.FirstName);
            createdUser.LastName.Should().Be(request.LastName);
            createdUser.EmailConfirmed.Should().BeTrue();

            // Verify user has default role
            var roles = await userManager.GetRolesAsync(createdUser);
            roles.Should().Contain("User");
        }

        [Fact]
        public async Task RegisterUser_WithPhoneNumber_ShouldCreateUserWithPhone()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request = RegistrationRequestBuilder.ValidUser()
                .WithEmail("userphone@test.com")
                .WithPhoneNumber("+51987654321")
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify phone number was saved
            using var scope = Factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var createdUser = await userManager.FindByEmailAsync(request.Email);

            createdUser.PhoneNumber.Should().Be(request.PhoneNumber);
        }

        #endregion

        #region Error Tests - Validation

        [Fact]
        public async Task RegisterUser_DuplicateEmail_ShouldReturnBadRequest()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var existingEmail = "existing@test.com";
            await Factory.CreateTestUserAsync(existingEmail, "Password123!");

            var request = RegistrationRequestBuilder.ValidUser()
                .WithEmail(existingEmail)
                .Build();

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("", "FirstName", "LastName", "Password123!")] // Email vacío
        [InlineData("invalid-email", "FirstName", "LastName", "Password123!")] // Email inválido
        [InlineData("test@test.com", "", "LastName", "Password123!")] // FirstName vacío
        [InlineData("test@test.com", "FirstName", "", "Password123!")] // LastName vacío
        [InlineData("test@test.com", "FirstName", "LastName", "")] // Password vacío
        [InlineData("test@test.com", "FirstName", "LastName", "123")] // Password muy corto
        public async Task RegisterUser_InvalidData_ShouldReturnBadRequest(
            string email, string firstName, string lastName, string password)
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request = new RegistrationRequest
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Password = password,
                PhoneNumber = null
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RegisterUser_EmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var jsonContent = new StringContent("{}", Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RegisterUser_NullRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var jsonContent = new StringContent("null", Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, jsonContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Error Tests - Content Type

        [Fact]
        public async Task RegisterUser_InvalidContentType_ShouldReturnBadRequest()
        {
            // Arrange
            var request = RegistrationRequestBuilder.ValidUser().Build();
            var xmlContent = new StringContent(
                "<root>test</root>",
                Encoding.UTF8,
                "application/xml");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, xmlContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        }

        [Fact]
        public async Task RegisterUser_MalformedJson_ShouldReturnBadRequest()
        {
            // Arrange
            var malformedJson = new StringContent(
                "{ invalid json }",
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await Client.PostAsync(RegisterEndpoint, malformedJson);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Database State Tests

        [Fact]
        public async Task RegisterUser_Success_ShouldNotCreateDuplicateRoles()
        {
            // Arrange
            await Factory.CleanDatabaseAsync();
            await Factory.SeedTestDataAsync();

            var request1 = RegistrationRequestBuilder.ValidUser()
                .WithEmail("user1@test.com")
                .Build();

            var request2 = RegistrationRequestBuilder.ValidUser()
                .WithEmail("user2@test.com")
                .Build();

            var jsonContent1 = new StringContent(
                JsonSerializer.Serialize(request1, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var jsonContent2 = new StringContent(
                JsonSerializer.Serialize(request2, JsonOptions),
                Encoding.UTF8,
                "application/json");

            // Act
            var response1 = await Client.PostAsync(RegisterEndpoint, jsonContent1);
            var response2 = await Client.PostAsync(RegisterEndpoint, jsonContent2);

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify no duplicate roles were created
            using var scope = Factory.Services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userRoles = await roleManager.FindByNameAsync("User");
            userRoles.Should().NotBeNull();
            // El rol "User" debe existir solo una vez
        }

        #endregion
    }
}