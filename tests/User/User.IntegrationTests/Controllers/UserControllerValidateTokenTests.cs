using FluentAssertions;
using Moq;
using Shared.Core.Dtos;
using System.Net;
using System.Text.Json;
using User.IntegrationTests.Builders;
using User.IntegrationTests.Common;
using User.IntegrationTests.Extensions;
using User.IntegrationTests.Fixtures;
using Xunit;

namespace User.IntegrationTests.Controllers
{
    [Collection("Sequential")] // Evitar conflictos de environment variables
    public class UserControllerValidateTokenTests : BaseIntegrationTest
    {
        private const string ValidateTokenEndpoint = "/api/user/validate-token";
        private const string ValidToken = "valid-jwt-token";
        private const string InvalidToken = "invalid-jwt-token";

        public UserControllerValidateTokenTests(CustomWebApplicationFactory<Program> factory)
            : base(factory) { }

        #region Success Tests - Non-Kubernetes Environment

        [Fact]
        public async Task ValidateToken_ValidToken_NonKubernetesEnvironment_ShouldReturnOkWithCompleteTokenData()
        {
            // Arrange
            SetKubernetesEnvironment(false);
            var expectedResult = TokenValidationResultBuilder.ValidUserToken();

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ReturnsAsync(expectedResult);

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TokenValidationDecoded>(content, JsonOptions);

            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.UserId.Should().Be(expectedResult.UserId);
            result.Email.Should().Be(expectedResult.Email);
            result.Roles.Should().BeEquivalentTo(expectedResult.Roles);

            // Verify mock was called
            MockExternalAuthService.Verify(x => x.ValidateTokenAsync(ValidToken), Times.Once);
        }

        [Fact]
        public async Task ValidateToken_AdminToken_NonKubernetesEnvironment_ShouldReturnCompleteAdminData()
        {
            // Arrange
            SetKubernetesEnvironment(false);
            var expectedResult = TokenValidationResultBuilder.ValidAdminToken();

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ReturnsAsync(expectedResult);

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TokenValidationDecoded>(content, JsonOptions);

            result.Roles.Should().Contain("Admin");
            result.Roles.Should().Contain("User");
        }

        #endregion

        #region Success Tests - Kubernetes Environment

        [Fact]
        public async Task ValidateToken_ValidToken_KubernetesEnvironment_ShouldReturnOkWithCustomHeaders()
        {
            // Arrange
            SetKubernetesEnvironment(true);
            var expectedResult = TokenValidationResultBuilder.ValidUserToken();

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ReturnsAsync(expectedResult);

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify Kubernetes-specific headers
            response.Headers.Should().ContainKey("x-user-id");
            response.Headers.Should().ContainKey("x-user-email");
            response.Headers.Should().ContainKey("x-user-roles");

            response.Headers.GetValues("x-user-id").First().Should().Be(expectedResult.UserId);
            response.Headers.GetValues("x-user-email").First().Should().Be(expectedResult.Email);
            response.Headers.GetValues("x-user-roles").First().Should().Be(string.Join(",", expectedResult.Roles));

            // Verify simplified response body
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("success");
            content.Should().Contain("Token valid");
        }

        #endregion

        #region Error Tests - Authorization

        [Theory]
        [InlineData(true)]  // Kubernetes environment
        [InlineData(false)] // Non-Kubernetes environment
        public async Task ValidateToken_NoAuthorizationHeader_ShouldReturnUnauthorized(bool isKubernetesEnvironment)
        {
            // Arrange
            SetKubernetesEnvironment(isKubernetesEnvironment);

            // Act
            var response = await Client.GetWithoutAuthorizationAsync(ValidateTokenEndpoint);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            if (isKubernetesEnvironment)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().Contain("Authentication required");
                content.Should().Contain("UNAUTHORIZED");
            }
        }

        [Fact]
        public async Task ValidateToken_InvalidAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            SetKubernetesEnvironment(false);

            // ← AGREGAR ESTA LÍNEA:
            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync("invalid-header"))
                .ReturnsAsync(TokenValidationResultBuilder.InvalidToken());

            // Act
            var response = await GetAsync(ValidateTokenEndpoint, request =>
            {
                request.Headers.Add("Authorization", "Bearer invalid-header");
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        [Fact]
        public async Task ValidateToken_BasicAuthInsteadOfBearer_ShouldReturnUnauthorized()
        {
            // Arrange
            SetKubernetesEnvironment(false);

            // Act - Basic Auth cuando se espera Bearer
            var response = await GetAsync(ValidateTokenEndpoint, request =>
            {
                request.Headers.Add("Authorization", "Basic dGVzdDp0ZXN0");
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ValidateToken_MalformedAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            SetKubernetesEnvironment(false);

            // Act - Header malformado sin esquema
            var response = await GetAsync(ValidateTokenEndpoint, request =>
            {
                request.Headers.Add("Authorization", "just-a-token-without-scheme");
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Error Tests - Invalid Token

        [Theory]
        [InlineData(true)]  // Kubernetes environment
        [InlineData(false)] // Non-Kubernetes environment
        public async Task ValidateToken_InvalidToken_ShouldReturnUnauthorized(bool isKubernetesEnvironment)
        {
            // Arrange
            SetKubernetesEnvironment(isKubernetesEnvironment);
            var invalidResult = TokenValidationResultBuilder.InvalidToken();

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(InvalidToken))
                .ReturnsAsync(invalidResult);

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, InvalidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            if (isKubernetesEnvironment)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().Contain("Authentication required");
                content.Should().Contain("UNAUTHORIZED");
            }
        }

        #endregion

        #region Error Tests - Service Exceptions

        [Theory]
        [InlineData(true)]  // Kubernetes environment
        [InlineData(false)] // Non-Kubernetes environment
        public async Task ValidateToken_HttpRequestException_ShouldReturnServiceUnavailable(bool isKubernetesEnvironment)
        {
            // Arrange
            SetKubernetesEnvironment(isKubernetesEnvironment);

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Authentication service unavailable");

            if (isKubernetesEnvironment)
            {
                content.Should().Contain("AUTH_SERVICE_UNAVAILABLE");
            }
        }

        [Fact]
        public async Task ValidateToken_TaskCanceledException_ShouldReturnServiceUnavailable()
        {
            // Arrange
            SetKubernetesEnvironment(false);

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ThrowsAsync(new TaskCanceledException("Operation timed out"));

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        [Theory]
        [InlineData(true)]  // Kubernetes environment
        [InlineData(false)] // Non-Kubernetes environment
        public async Task ValidateToken_GenericException_ShouldReturnInternalServerError(bool isKubernetesEnvironment)
        {
            // Arrange
            SetKubernetesEnvironment(isKubernetesEnvironment);

            MockExternalAuthService
                .Setup(x => x.ValidateTokenAsync(ValidToken))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var response = await Client.GetWithBearerTokenAsync(ValidateTokenEndpoint, ValidToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

            if (isKubernetesEnvironment)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().Contain("AUTH_SERVICE_ERROR");
            }
        }

        #endregion

        #region Helper Extension for this class

        private async Task<HttpResponseMessage> GetAsync(string requestUri, Action<HttpRequestMessage> configureRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            configureRequest(request);
            return await Client.SendAsync(request);
        }

        #endregion
    }
}