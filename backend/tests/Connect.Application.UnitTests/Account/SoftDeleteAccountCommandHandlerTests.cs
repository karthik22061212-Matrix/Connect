using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Account.Commands.SoftDeleteAccount;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Account;

public class SoftDeleteAccountCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly SoftDeleteAccountCommandHandler _handler;

    public SoftDeleteAccountCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc));

        _handler = new SoftDeleteAccountCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_SoftDeletesAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            UserId = "john_doe",
            Email = "john@example.com",
            IsDeleted = false,
            PresenceStatus = PresenceStatus.Online
        };

        _currentUserServiceMock.Setup(c => c.UserId).Returns(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(new SoftDeleteAccountCommand(), CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.NotNull(user.ReactivationDeadline);
        Assert.Equal(PresenceStatus.Offline, user.PresenceStatus);
        Assert.Equal(user.DeletedAt.Value.AddDays(60), user.ReactivationDeadline.Value);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new SoftDeleteAccountCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedUser_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            IsDeleted = true
        };

        _currentUserServiceMock.Setup(c => c.UserId).Returns(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new SoftDeleteAccountCommand(), CancellationToken.None));
    }
}
