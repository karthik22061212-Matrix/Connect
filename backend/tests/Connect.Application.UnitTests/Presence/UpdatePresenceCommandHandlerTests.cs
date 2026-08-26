using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Presence.Commands.UpdatePresence;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Presence;

public class UpdatePresenceCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly UpdatePresenceCommandHandler _handler;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public UpdatePresenceCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new UpdatePresenceCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _presenceTrackerMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesPresenceInDbAndTracker()
    {
        var user = new User { Id = _currentUserId, PresenceStatus = PresenceStatus.Offline };
        _userRepoMock.Setup(r => r.GetByIdAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdatePresenceCommand(PresenceStatus.Busy);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(PresenceStatus.Busy, result.Status);
        Assert.Equal(PresenceStatus.Busy, user.PresenceStatus);

        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_currentUserId, PresenceStatus.Busy), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
