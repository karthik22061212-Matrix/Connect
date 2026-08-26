using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Auth.Commands.RegisterUser;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Auth;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>())).Returns("hashed_password");
        _jwtTokenGeneratorMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("valid_jwt_token");

        _handler = new RegisterUserCommandHandler(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenGeneratorMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_RegistersUserAndReturnsToken()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var command = new RegisterUserCommand("john_doe", "john@example.com", "Password123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("john_doe", result.UserId);
        Assert.Equal("john@example.com", result.Email);
        Assert.Equal("valid_jwt_token", result.Token);

        _userRepoMock.Verify(r => r.Add(It.Is<User>(u => u.UserId == "john_doe" && u.Email == "john@example.com")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateUserId_ThrowsConflictException()
    {
        // Arrange
        var existingUser = new User { UserId = "john_doe", Email = "other@example.com" };
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { existingUser });

        var command = new RegisterUserCommand("JOHN_DOE", "john@example.com", "Password123!");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
