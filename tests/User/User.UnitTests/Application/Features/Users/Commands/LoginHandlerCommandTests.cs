namespace User.UnitTests.Application.Features.Users.Commands;

public class LoginHandlerCommandTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly LoginHandlerCommand _handler;

    public LoginHandlerCommandTests()
    {
        _mockAuthService = new Mock<IAuthService>();

        // Setup UserManager mock
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        _handler = new LoginHandlerCommand(_mockAuthService.Object, _mockUserManager.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Authentication")]
    public async Task Handle_ValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var request = CreateLoginCommand("test@example.com", "ValidPassword123!");
        var user = CreateApplicationUser("user-123", "test@example.com", "testuser");
        var expectedToken = "jwt-token-example";

        SetupUserManagerFindByEmail(request.Request.Email, user);
        SetupAuthServicePasswordSignIn(request.Request.Email, request.Request.Password, SignInResult.Success);
        SetupAuthServiceGenerateToken(user, expectedToken);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be(expectedToken);
        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.UserName.Should().Be(user.UserName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Authentication")]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = CreateLoginCommand("nonexistent@example.com", "Password123!");
        SetupUserManagerFindByEmail(request.Request.Email, null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(request, CancellationToken.None));

        exception.Message.Should().Be("Credenciales inválidas.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Authentication")]
    public async Task Handle_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = CreateLoginCommand("test@example.com", "WrongPassword");
        var user = CreateApplicationUser("user-123", "test@example.com", "testuser");

        SetupUserManagerFindByEmail(request.Request.Email, user);
        SetupAuthServicePasswordSignIn(request.Request.Email, request.Request.Password, SignInResult.Failed);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(request, CancellationToken.None));

        exception.Message.Should().Be("Credenciales inválidas.");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Authentication")]
    [InlineData("", "Password123!")]
    [InlineData("test@example.com", "")]
    [InlineData("", "")]
    public async Task Handle_EmptyCredentials_ThrowsUnauthorizedAccessException(string email, string password)
    {
        // Arrange
        var request = CreateLoginCommand(email, password);
        SetupUserManagerFindByEmail(email, null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(request, CancellationToken.None));

        exception.Message.Should().Be("Credenciales inválidas.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Authentication")]
    public async Task Handle_LockedOutUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = CreateLoginCommand("test@example.com", "Password123!");
        var user = CreateApplicationUser("user-123", "test@example.com", "testuser");

        SetupUserManagerFindByEmail(request.Request.Email, user);
        SetupAuthServicePasswordSignIn(request.Request.Email, request.Request.Password, SignInResult.LockedOut);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(request, CancellationToken.None));

        exception.Message.Should().Be("Credenciales inválidas.");
    }

    #region Helper Methods

    private static LoginCommand CreateLoginCommand(string email, string password) =>
        new()
        {
            Request = new LoginRequest
            {
                Email = email,
                Password = password
            }
        };

    private static ApplicationUser CreateApplicationUser(string id, string email, string userName) =>
        new()
        {
            Id = id,
            Email = email,
            UserName = userName
        };

    private void SetupUserManagerFindByEmail(string email, ApplicationUser? user)
    {
        _mockUserManager
            .Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
    }

    private void SetupAuthServicePasswordSignIn(string email, string password, SignInResult result)
    {
        _mockAuthService
            .Setup(x => x.PasswordSignInAsync(email, password, false, false))
            .ReturnsAsync(result);
    }

    private void SetupAuthServiceGenerateToken(ApplicationUser user, string token)
    {
        _mockAuthService
            .Setup(x => x.GenerateJwtTokenAsync(user, 0))
            .ReturnsAsync(token);
    }

    #endregion
}