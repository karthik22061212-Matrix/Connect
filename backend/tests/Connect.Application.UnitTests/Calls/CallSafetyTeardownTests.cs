using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Blocking.Commands.BlockUser;
using Connect.Application.Features.Calls.Commands.InitiateCall;
using Connect.Application.Features.Reports.Commands.ReportUser;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class CallSafetyTeardownTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<IRepository<Report>> _reportRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<IRepository<ConnectRequest>> _connectRequestRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock = new();
    private readonly Mock<IHubContext<CallHub, ICallHubClient>> _hubContextMock = new();
    private readonly Mock<IHubClients<ICallHubClient>> _hubClientsMock = new();
    private readonly Mock<ICallHubClient> _clientProxyMock = new();

    private readonly CallRealtimeNotifier _notifier;
    private readonly BlockUserCommandHandler _blockHandler;
    private readonly ReportUserCommandHandler _reportHandler;
    private readonly InitiateCallCommandHandler _initiateHandler;

    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    public CallSafetyTeardownTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Reports).Returns(_reportRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_connectRequestRepoMock.Object);

        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_utcNow);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userAId);
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_clientProxyMock.Object);

        _notifier = new CallRealtimeNotifier(
            _hubContextMock.Object,
            _presenceTrackerMock.Object,
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object);

        _blockHandler = new BlockUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object,
            _notifier);

        _reportHandler = new ReportUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object,
            _notifier);

        _initiateHandler = new InitiateCallCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _presenceTrackerMock.Object,
            _dateTimeProviderMock.Object,
            _pushNotificationServiceMock.Object);
    }

    [Fact]
    public async Task BlockUser_WithInFlightAcceptedCall_TerminatesCallAndResetsPresenceToOnline()
    {
        // Arrange
        var targetUser = new User { Id = _userBId, UserId = "userB", PresenceStatus = PresenceStatus.Busy };
        var blockerUser = new User { Id = _userAId, UserId = "userA", PresenceStatus = PresenceStatus.Busy };

        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockerUser);

        _blockRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var acceptedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Accepted,
            TimeoutDeadline = _utcNow.AddMinutes(1)
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { acceptedCall });

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        // Act
        var result = await _blockHandler.Handle(new BlockUserCommand(_userBId), CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(CallStatus.Failed, acceptedCall.Status);
        Assert.Equal(_utcNow, acceptedCall.EndedAt);
        Assert.Null(acceptedCall.TimeoutDeadline);

        // SignalR CallEnded dispatched to both users
        _clientProxyMock.Verify(c => c.CallEnded(acceptedCall.Id), Times.Once);

        // Presence reset from Busy back to Online on presence tracker and DB
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userAId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userBId, PresenceStatus.Online), Times.Once);
        Assert.Equal(PresenceStatus.Online, blockerUser.PresenceStatus);
        Assert.Equal(PresenceStatus.Online, targetUser.PresenceStatus);

        _blockRepoMock.Verify(r => r.Add(It.Is<Block>(b => b.BlockerUserId == _userAId && b.BlockedUserId == _userBId)), Times.Once);
    }

    [Fact]
    public async Task ReportUser_WithInFlightAcceptedCall_TerminatesCallAndResetsPresenceToOnline()
    {
        // Arrange
        var reportedUser = new User { Id = _userBId, UserId = "userB", PresenceStatus = PresenceStatus.Busy };
        var reporterUser = new User { Id = _userAId, UserId = "userA", PresenceStatus = PresenceStatus.Busy };

        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporterUser);

        _reportRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Report, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Report?)null);

        var acceptedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userBId,
            CalleeId = _userAId,
            Status = CallStatus.Accepted
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { acceptedCall });

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        // Act
        var reportId = await _reportHandler.Handle(new ReportUserCommand(_userBId, "Harassment", "Note"), CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, reportId);
        Assert.Equal(CallStatus.Failed, acceptedCall.Status);
        _clientProxyMock.Verify(c => c.CallEnded(acceptedCall.Id), Times.Once);

        // Both participants' presence reset to Online
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userAId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userBId, PresenceStatus.Online), Times.Once);
        Assert.Equal(PresenceStatus.Online, reporterUser.PresenceStatus);
        Assert.Equal(PresenceStatus.Online, reportedUser.PresenceStatus);
    }

    [Fact]
    public async Task InitiateCall_WhenCallerBlockedByCallee_SimulatesMissedOfflineWithoutDisclosingBlock()
    {
        // Arrange
        var caller = new User { Id = _userAId, UserId = "caller" };
        var callee = new User { Id = _userBId, UserId = "callee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        // Callee blocked caller
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block> { new Block { BlockerUserId = _userBId, BlockedUserId = _userAId } });

        // Act
        var result = await _initiateHandler.Handle(new InitiateCallCommand(_userBId), CancellationToken.None);

        // Assert: Simulated Missed / Offline without leaking block
        Assert.NotNull(result);
        Assert.Equal(CallStatus.Missed, result.Status);
        Assert.Equal(MissedReason.Offline, result.MissedReason);
        Assert.Equal(_userAId, result.CallerId);
        Assert.Equal(_userBId, result.CalleeId);

        // Does NOT create database call record
        _callRepoMock.Verify(r => r.Add(It.IsAny<Call>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        // Does NOT send push notification to callee
        _pushNotificationServiceMock.Verify(p => p.SendMissedCallNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<MissedReason>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitiateCall_WhenCallerBlockedCallee_ThrowsConflictException()
    {
        // Arrange
        var caller = new User { Id = _userAId, UserId = "caller" };
        var callee = new User { Id = _userBId, UserId = "callee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        // Caller blocked callee
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block> { new Block { BlockerUserId = _userAId, BlockedUserId = _userBId } });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _initiateHandler.Handle(new InitiateCallCommand(_userBId), CancellationToken.None));
        Assert.Contains("You have blocked this user", ex.Message);
    }

    [Fact]
    public async Task BlockUser_WithRingingCall_TerminatesRingingCallAndEmitsCallEnded()
    {
        // Arrange
        var targetUser = new User { Id = _userBId, UserId = "userB", PresenceStatus = PresenceStatus.Busy };
        var blockerUser = new User { Id = _userAId, UserId = "userA", PresenceStatus = PresenceStatus.Busy };
        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockerUser);

        _blockRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ringingCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Ringing,
            TimeoutDeadline = _utcNow.AddSeconds(15)
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { ringingCall });

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        // Act
        var result = await _blockHandler.Handle(new BlockUserCommand(_userBId), CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(CallStatus.Failed, ringingCall.Status);
        Assert.Equal(_utcNow, ringingCall.EndedAt);
        Assert.Null(ringingCall.TimeoutDeadline);
        _clientProxyMock.Verify(c => c.CallEnded(ringingCall.Id), Times.Once);

        // Presence reset verified on Ringing call termination
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userAId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userBId, PresenceStatus.Online), Times.Once);
        Assert.Equal(PresenceStatus.Online, blockerUser.PresenceStatus);
        Assert.Equal(PresenceStatus.Online, targetUser.PresenceStatus);
    }
}
