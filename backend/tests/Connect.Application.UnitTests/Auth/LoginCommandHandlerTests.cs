using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Auth.Commands.Login;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _jwtTokenGeneratorMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("valid_jwt_token");
        _refreshTokenServiceMock.Setup(r => r.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync("valid_refresh_token");
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc));

        _handler = new LoginCommandHandler(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenGeneratorMock.Object,
            _refreshTokenServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = "john_doe",
            Email = "john@example.com",
            PasswordHash = "hashed_password"
        };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword(user, "hashed_password", "Password123!"))
            .Returns(true);

        var command = new LoginCommand("john_doe", "Password123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("john_doe", result.UserId);
        Assert.Equal("valid_jwt_token", result.Token);
    }

    [Fact]
    public async Task Handle_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            UserId = "john_doe",
            Email = "john@example.com",
            PasswordHash = "hashed_password"
        };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword(user, "hashed_password", "WrongPassword"))
            .Returns(false);

        var command = new LoginCommand("john_doe", "WrongPassword");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedUserWithinWindow_ReactivatesAndReturnsAuthResponse()
    {
        // Arrange
        var now = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = "john_doe",
            Email = "john@example.com",
            PasswordHash = "hashed_password",
            IsDeleted = true,
            DeletedAt = now.AddDays(-10),
            ReactivationDeadline = now.AddDays(50)
        };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword(user, "hashed_password", "Password123!"))
            .Returns(true);

        var command = new LoginCommand("john_doe", "Password123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.ReactivationDeadline);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SoftDeletedUserPastWindow_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var now = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = "john_doe",
            Email = "john@example.com",
            PasswordHash = "hashed_password",
            IsDeleted = true,
            DeletedAt = now.AddDays(-70),
            ReactivationDeadline = now.AddDays(-10)
        };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword(user, "hashed_password", "Password123!"))
            .Returns(true);

        var command = new LoginCommand("john_doe", "Password123!");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("passed the 60-day reactivation deadline", ex.Message);
    }
}
