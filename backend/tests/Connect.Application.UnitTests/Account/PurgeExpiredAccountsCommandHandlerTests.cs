using System.Linq.Expressions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Account.Commands.PurgeExpiredAccounts;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Account;

public class PurgeExpiredAccountsCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly PurgeExpiredAccountsCommandHandler _handler;

    public PurgeExpiredAccountsCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc));

        _handler = new PurgeExpiredAccountsCommandHandler(
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_AccountsPast60DayWindow_PurgesExpiredAccounts()
    {
        // Arrange
        var now = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var expiredUser1 = new User { Id = Guid.NewGuid(), IsDeleted = true, ReactivationDeadline = now.AddDays(-1) };
        var expiredUser2 = new User { Id = Guid.NewGuid(), IsDeleted = true, ReactivationDeadline = now.AddDays(-10) };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { expiredUser1, expiredUser2 });

        // Act
        var result = await _handler.Handle(new PurgeExpiredAccountsCommand(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result);
        _userRepoMock.Verify(r => r.Remove(expiredUser1), Times.Once);
        _userRepoMock.Verify(r => r.Remove(expiredUser2), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoExpiredAccounts_ReturnsZero()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _handler.Handle(new PurgeExpiredAccountsCommand(), CancellationToken.None);

        // Assert
        Assert.Equal(0, result);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
