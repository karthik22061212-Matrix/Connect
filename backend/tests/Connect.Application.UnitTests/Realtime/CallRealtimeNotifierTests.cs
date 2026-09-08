using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Connect.Application.UnitTests.Realtime;

public class CallRealtimeNotifierTests
{
    private readonly Mock<IHubContext<CallHub, ICallHubClient>> _hubContextMock = new();
    private readonly Mock<IHubClients<ICallHubClient>> _hubClientsMock = new();
    private readonly Mock<ICallHubClient> _clientProxyMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly CallRealtimeNotifier _notifier;

    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    public CallRealtimeNotifierTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_clientProxyMock.Object);

        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_utcNow);

        _notifier = new CallRealtimeNotifier(
            _hubContextMock.Object,
            _presenceTrackerMock.Object,
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task TerminateActiveCallsBetweenUsersAsync_ActiveAndRingingCalls_FailsCallsAndEmitsCallEnded()
    {
        var ringingCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Ringing,
            TimeoutDeadline = _utcNow.AddSeconds(30),
            TimeoutType = CallTimeoutType.Ring
        };

        var acceptedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userBId,
            CalleeId = _userAId,
            Status = CallStatus.Accepted,
            TimeoutDeadline = _utcNow.AddSeconds(60),
            TimeoutType = CallTimeoutType.Reconnect
        };

        var unrelatedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = Guid.NewGuid(),
            Status = CallStatus.Ringing
        };

        var completedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Completed
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { ringingCall, acceptedCall, unrelatedCall, completedCall });

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a-1" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b-1" });

        await _notifier.TerminateActiveCallsBetweenUsersAsync(_userAId, _userBId, "UserReported", CancellationToken.None);

        Assert.Equal(CallStatus.Failed, ringingCall.Status);
        Assert.Equal(_utcNow, ringingCall.EndedAt);
        Assert.Null(ringingCall.TimeoutDeadline);
        Assert.Null(ringingCall.TimeoutType);

        Assert.Equal(CallStatus.Failed, acceptedCall.Status);
        Assert.Equal(_utcNow, acceptedCall.EndedAt);
        Assert.Null(acceptedCall.TimeoutDeadline);
        Assert.Null(acceptedCall.TimeoutType);

        Assert.Equal(CallStatus.Ringing, unrelatedCall.Status);
        Assert.Equal(CallStatus.Completed, completedCall.Status);

        _clientProxyMock.Verify(c => c.CallEnded(ringingCall.Id), Times.Once);
        _clientProxyMock.Verify(c => c.CallEnded(acceptedCall.Id), Times.Once);
        _clientProxyMock.Verify(c => c.CallEnded(unrelatedCall.Id), Times.Never);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TerminateActiveCallsBetweenUsersAsync_NoActiveCalls_DoesNotSaveOrEmit()
    {
        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call>());

        await _notifier.TerminateActiveCallsBetweenUsersAsync(_userAId, _userBId, "UserReported", CancellationToken.None);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _clientProxyMock.Verify(c => c.CallEnded(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task NotifyConnectionRemovedAsync_EmitsConnectionRemovedToBothParticipants()
    {
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        await _notifier.NotifyConnectionRemovedAsync(_userAId, _userBId, CancellationToken.None);

        _clientProxyMock.Verify(c => c.ConnectionRemoved(_userBId), Times.Once);
        _clientProxyMock.Verify(c => c.ConnectionRemoved(_userAId), Times.Once);
    }

    [Fact]
    public async Task TerminateActiveCallsBetweenUsersAsync_AcceptedCall_ResetsParticipantPresenceFromBusyToOnline()
    {
        var acceptedCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Accepted
        };

        var userA = new User { Id = _userAId, PresenceStatus = PresenceStatus.Busy };
        var userB = new User { Id = _userBId, PresenceStatus = PresenceStatus.Busy };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { acceptedCall });
        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userB);

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        await _notifier.TerminateActiveCallsBetweenUsersAsync(_userAId, _userBId, "UserBlocked", CancellationToken.None);

        Assert.Equal(CallStatus.Failed, acceptedCall.Status);
        Assert.Equal(PresenceStatus.Online, userA.PresenceStatus);
        Assert.Equal(PresenceStatus.Online, userB.PresenceStatus);

        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userAId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userBId, PresenceStatus.Online), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TerminateActiveCallsBetweenUsersAsync_RingingCall_ResetsParticipantPresenceFromBusyToOnline()
    {
        var ringingCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userAId,
            CalleeId = _userBId,
            Status = CallStatus.Ringing
        };

        var userA = new User { Id = _userAId, PresenceStatus = PresenceStatus.Busy };
        var userB = new User { Id = _userBId, PresenceStatus = PresenceStatus.Busy };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { ringingCall });
        _userRepoMock.Setup(r => r.GetByIdAsync(_userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);
        _userRepoMock.Setup(r => r.GetByIdAsync(_userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userB);

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userAId))
            .ReturnsAsync(new List<string> { "conn-a" });
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(_userBId))
            .ReturnsAsync(new List<string> { "conn-b" });

        await _notifier.TerminateActiveCallsBetweenUsersAsync(_userAId, _userBId, "UserBlocked", CancellationToken.None);

        Assert.Equal(CallStatus.Failed, ringingCall.Status);
        Assert.Equal(PresenceStatus.Online, userA.PresenceStatus);
        Assert.Equal(PresenceStatus.Online, userB.PresenceStatus);

        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userAId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_userBId, PresenceStatus.Online), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
