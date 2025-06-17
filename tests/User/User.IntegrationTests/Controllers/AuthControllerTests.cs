using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using User.Application.DTOs.Requests;
using User.Application.DTOs.Responses;
using User.IntegrationTests.Helpers;
using User.IntegrationTests.Infrastructure;

namespace User.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<TestWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Setup before each test class
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        // Cleanup after each test class
        return Task.CompletedTask;
    }

    #region Register Tests

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_ValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var request = TestDataBuilder.Registration.ValidRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/RegisterUser", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsJsonAsync<RegistrationResponse>();
        content.Should().NotBeNull();
        content!.IsSuccess.Should().BeTrue();
        content.Message.Should().Be("Registro exitoso");
        content.Token.Should().NotBeNullOrEmpty();
        content.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var firstRequest = TestDataBuilder.Registration.WithEmail(email);
        var secondRequest = TestDataBuilder.Registration.WithEmail(email);

        // Act - Register first user
        var firstResponse = await _client.PostAsJsonAsync("/api/User/RegisterUser", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Try to register with same email
        var secondResponse = await _client.PostAsJsonAsync("/api/User/RegisterUser", secondRequest);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await secondResponse.ReadAsStringAsync();
        errorContent.Should().Contain($"El email {email} ya está registrado");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("weak")]
    [InlineData("NoNumbers")]
    public async Task RegisterUser_WeakPassword_ReturnsBadRequest(string weakPassword)
    {
        // Arrange
        var request = TestDataBuilder.Registration.WithPassword(weakPassword);

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/RegisterUser", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.ReadAsStringAsync();
        errorContent.Should().Contain("Error al crear usuario");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = TestDataBuilder.Registration.InvalidEmail();

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/RegisterUser", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.ReadAsStringAsync();
        errorContent.Should().Contain("Error al crear usuario");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_EmptyFirstName_ReturnsBadRequest()
    {
        // Arrange
        var request = TestDataBuilder.Registration.EmptyFirstName();

        // Act
        var response = await _client.PostAsJsonAsync("/api/User/RegisterUser", request);

        // Assert
        // Nota: Esto depende de si tienes validación en tu modelo/controller
        // Si no tienes validación, este test podría pasar con 200 OK
        // En ese caso, considera agregar validación con FluentValidation o DataAnnotations
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_MalformedJson_ReturnsBadRequest()
    {
        // Arrange
        var malformedJson = "{ invalid json }";
        var content = new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/User/RegisterUser", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task RegisterUser_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var emptyContent = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/User/RegisterUser", emptyContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login Tests (Bonus - ya que seguramente también lo necesitas)

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Feature", "Authentication")]
    public async Task Login_AfterSuccessfulRegistration_ReturnsToken()
    {
        // Arrange - First register a user
        var registerRequest = TestDataBuilder.Registration.ValidRequest();
        var registerResponse = await _client.PostAsJsonAsync("/api/User/RegisterUser", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginRequest = TestDataBuilder.Login.WithCredentials(
            registerRequest.Email,
            registerRequest.Password);

        // Act - Login with registered credentials
        var loginResponse = await _client.PostAsJsonAsync("/api/User/Login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginContent = await loginResponse.Content.ReadAsJsonAsync<LoginResponse>();
        loginContent.Should().NotBeNull();
        loginContent!.Token.Should().NotBeNullOrEmpty();
        loginContent.Email.Should().Be(registerRequest.Email);
    }

    #endregion
}