using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.InitiateCall;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class InitiateCallCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock = new();
    private readonly InitiateCallCommandHandler _handler;

    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _calleeId = Guid.NewGuid();

    public InitiateCallCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(_callerId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        _handler = new InitiateCallCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _presenceTrackerMock.Object,
            _dateTimeProviderMock.Object,
            _pushNotificationServiceMock.Object);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var command = new InitiateCallCommand(_calleeId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SelfCall_ThrowsConflictException()
    {
        var command = new InitiateCallCommand(_callerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CallerNotFound_ThrowsNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new InitiateCallCommand(_calleeId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CalleeNotFound_ThrowsNotFoundException()
    {
        var caller = new User { Id = _callerId, UserId = "caller" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_calleeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new InitiateCallCommand(_calleeId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Blocked_ThrowsForbiddenAccessException()
    {
        var caller = new User { Id = _callerId, UserId = "caller" };
        var callee = new User { Id = _calleeId, UserId = "callee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_calleeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        var blocks = new List<Block>
        {
            new Block { BlockerUserId = _calleeId, BlockedUserId = _callerId }
        };
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(blocks);

        var command = new InitiateCallCommand(_calleeId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotConnected_ThrowsForbiddenAccessException()
    {
        var caller = new User { Id = _callerId, UserId = "caller" };
        var callee = new User { Id = _calleeId, UserId = "callee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_calleeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());

        var command = new InitiateCallCommand(_calleeId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OfflineCallee_ReturnsMissedCallOffline()
    {
        SetupValidConnection();

        _presenceTrackerMock.Setup(p => p.GetUserPresenceAsync(_calleeId))
            .ReturnsAsync(PresenceStatus.Offline);

        var command = new InitiateCallCommand(_calleeId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CallStatus.Missed, result.Status);
        Assert.Equal(MissedReason.Offline, result.MissedReason);
        _callRepoMock.Verify(r => r.Add(It.Is<Call>(c => c.Status == CallStatus.Missed && c.MissedReason == MissedReason.Offline)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BusyCallee_ReturnsMissedCallBusy()
    {
        SetupValidConnection();

        _presenceTrackerMock.Setup(p => p.GetUserPresenceAsync(_calleeId))
            .ReturnsAsync(PresenceStatus.Busy);

        var command = new InitiateCallCommand(_calleeId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CallStatus.Missed, result.Status);
        Assert.Equal(MissedReason.Busy, result.MissedReason);
        _callRepoMock.Verify(r => r.Add(It.Is<Call>(c => c.Status == CallStatus.Missed && c.MissedReason == MissedReason.Busy)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnlineCallee_ReturnsRingingCall()
    {
        SetupValidConnection();

        _presenceTrackerMock.Setup(p => p.GetUserPresenceAsync(_calleeId))
            .ReturnsAsync(PresenceStatus.Online);

        var command = new InitiateCallCommand(_calleeId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CallStatus.Ringing, result.Status);
        Assert.Null(result.MissedReason);
        Assert.NotNull(result.CallId);
        _callRepoMock.Verify(r => r.Add(It.Is<Call>(c => c.Status == CallStatus.Ringing)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupValidConnection()
    {
        var caller = new User { Id = _callerId, UserId = "caller" };
        var callee = new User { Id = _calleeId, UserId = "callee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_calleeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        var minId = _callerId.CompareTo(_calleeId) < 0 ? _callerId : _calleeId;
        var maxId = _callerId.CompareTo(_calleeId) < 0 ? _calleeId : _callerId;

        var connections = new List<Connection>
        {
            new Connection { Id = Guid.NewGuid(), UserAId = minId, UserBId = maxId }
        };

        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connections);
    }
}
