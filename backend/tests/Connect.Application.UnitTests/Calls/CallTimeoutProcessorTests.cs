using System.Linq.Expressions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.RecordNetworkDrop;
using Connect.Application.Features.Calls.Commands.RecordNetworkRestored;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using Connect.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class CallTimeoutProcessorTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IHubContext<CallHub, ICallHubClient>> _hubContextMock = new();
    private readonly Mock<IHubClients<ICallHubClient>> _hubClientsMock = new();
    private readonly Mock<ICallHubClient> _callHubClientMock = new();
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock = new();
    private readonly Mock<ILogger<CallTimeoutProcessor>> _loggerMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private readonly CallTimeoutProcessor _processor;
    private readonly DateTime _now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    public CallTimeoutProcessorTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_now);

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_callHubClientMock.Object);

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<string> { "conn-1" });

        _processor = new CallTimeoutProcessor(
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object,
            _presenceTrackerMock.Object,
            _hubContextMock.Object,
            _pushNotificationServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessExpiredTimeouts_ExpiredRingTimeout_FailsCallAsMissedNoAnswerAndNotifies()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId,
            Status = CallStatus.Ringing,
            TimeoutDeadline = _now.AddSeconds(-1), // Past deadline
            TimeoutType = CallTimeoutType.Ring
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { call });

        // Act
        await _processor.ProcessExpiredTimeoutsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CallStatus.Missed, call.Status);
        Assert.Equal(MissedReason.NoAnswer, call.MissedReason);
        Assert.Equal(_now, call.EndedAt);
        Assert.Null(call.TimeoutDeadline);
        Assert.Null(call.TimeoutType);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _callHubClientMock.Verify(c => c.CallTimeout(callId), Times.Once);
        _callHubClientMock.Verify(c => c.CalleeUnavailable(calleeId, "NoAnswer"), Times.Once);
        _callHubClientMock.Verify(c => c.CallEnded(callId), Times.Once);
        _pushNotificationServiceMock.Verify(p => p.SendMissedCallNotificationAsync(
            calleeId, callId, It.IsAny<string>(), MissedReason.NoAnswer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredTimeouts_ExpiredReconnectTimeout_FailsCallAsFailedConnectionFailedAndNotifies()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId,
            Status = CallStatus.Accepted,
            TimeoutDeadline = _now.AddSeconds(-1), // Past deadline
            TimeoutType = CallTimeoutType.Reconnect
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { call });

        // Act
        await _processor.ProcessExpiredTimeoutsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(CallStatus.Failed, call.Status);
        Assert.Equal(MissedReason.ConnectionFailed, call.MissedReason);
        Assert.Equal(_now, call.EndedAt);
        Assert.Null(call.TimeoutDeadline);
        Assert.Null(call.TimeoutType);

        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(callerId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(calleeId, PresenceStatus.Online), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _callHubClientMock.Verify(c => c.CallFailed(callId, It.Is<string>(s => s.Contains("Network drop timeout"))), Times.Once);
        _callHubClientMock.Verify(c => c.CallEnded(callId), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredTimeouts_AcceptedOrEndedCall_IsNotAffected_Idempotency()
    {
        // Arrange: Call was accepted before processor run
        var callId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId,
            Status = CallStatus.Accepted, // Was accepted by callee!
            TimeoutDeadline = _now.AddSeconds(-1), // Stale ring timeout deadline
            TimeoutType = CallTimeoutType.Ring
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { call });

        // Act
        await _processor.ProcessExpiredTimeoutsAsync(CancellationToken.None);

        // Assert: Status remains Accepted, NOT changed to Missed
        Assert.Equal(CallStatus.Accepted, call.Status);
        Assert.Null(call.TimeoutDeadline);
        Assert.Null(call.TimeoutType);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _callHubClientMock.Verify(c => c.CallTimeout(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RecordNetworkDrop_CalledTwiceInQuickSuccession_UpdatesSingleDeadline_FixesRT006()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId,
            Status = CallStatus.Accepted,
            TimeoutDeadline = null,
            TimeoutType = null
        };

        _callRepoMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(callerId);

        var handler = new RecordNetworkDropCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);

        // First call at T=0
        var result1 = await handler.Handle(new RecordNetworkDropCommand(callId), CancellationToken.None);
        var firstDeadline = call.TimeoutDeadline;

        Assert.Equal(callId, result1.CallId);
        Assert.Equal(_now.AddSeconds(10), firstDeadline);
        Assert.Equal(CallTimeoutType.Reconnect, call.TimeoutType);

        // Second call at T+2 seconds
        var t2 = _now.AddSeconds(2);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(t2);

        var result2 = await handler.Handle(new RecordNetworkDropCommand(callId), CancellationToken.None);
        var secondDeadline = call.TimeoutDeadline;

        // Assert: Same call entity updated with new single deadline (T2 + 10s = T+12s), no multiple timers
        Assert.Equal(callId, result2.CallId);
        Assert.Equal(t2.AddSeconds(10), secondDeadline);
        Assert.Equal(CallTimeoutType.Reconnect, call.TimeoutType);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RecordNetworkRestored_ClearsTimeoutDeadline()
    {
        // Arrange
        var callId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId,
            Status = CallStatus.Accepted,
            TimeoutDeadline = _now.AddSeconds(8),
            TimeoutType = CallTimeoutType.Reconnect
        };

        _callRepoMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(callerId);

        var handler = new RecordNetworkRestoredCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);

        // Act
        var result = await handler.Handle(new RecordNetworkRestoredCommand(callId), CancellationToken.None);

        // Assert
        Assert.Equal(callId, result.CallId);
        Assert.Null(call.TimeoutDeadline);
        Assert.Null(call.TimeoutType);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
