using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Presence.Queries.GetPresence;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Presence;

public class GetPresenceQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IPresenceTracker> _presenceTrackerMock;
    private readonly Mock<IPresenceVisibilityService> _presenceVisibilityServiceMock;
    private readonly GetPresenceQueryHandler _handler;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetPresenceQueryHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _presenceTrackerMock = new Mock<IPresenceTracker>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _presenceVisibilityServiceMock = new Mock<IPresenceVisibilityService>();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetPresenceQueryHandler(
            _unitOfWorkMock.Object,
            _presenceTrackerMock.Object,
            _currentUserServiceMock.Object,
            _presenceVisibilityServiceMock.Object);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);

        var query = new GetPresenceQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        var targetId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetPresenceQuery(targetId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotConnected_ThrowsForbiddenAccessException()
    {
        var targetId = Guid.NewGuid();
        var targetUser = new User { Id = targetId };

        _userRepoMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _presenceVisibilityServiceMock.Setup(p => p.CanViewPresenceAsync(targetId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new GetPresenceQuery(targetId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SelfLookup_ReturnsPresenceWithoutCheckingConnection()
    {
        var selfUser = new User { Id = _currentUserId, UpdatedAt = DateTime.UtcNow };
        _userRepoMock.Setup(r => r.GetByIdAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selfUser);

        _presenceTrackerMock.Setup(p => p.GetUserPresenceAsync(_currentUserId))
            .ReturnsAsync(PresenceStatus.Online);

        var query = new GetPresenceQuery(_currentUserId);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_currentUserId, result.UserId);
        Assert.Equal(PresenceStatus.Online, result.Status);
    }

    [Fact]
    public async Task Handle_ConnectedUser_ReturnsPresence()
    {
        var targetId = Guid.NewGuid();
        var targetUser = new User { Id = targetId, UpdatedAt = DateTime.UtcNow };

        _userRepoMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _presenceVisibilityServiceMock.Setup(p => p.CanViewPresenceAsync(targetId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _presenceTrackerMock.Setup(p => p.GetUserPresenceAsync(targetId))
            .ReturnsAsync(PresenceStatus.Busy);

        var query = new GetPresenceQuery(targetId);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(targetId, result.UserId);
        Assert.Equal(PresenceStatus.Busy, result.Status);
    }
}
