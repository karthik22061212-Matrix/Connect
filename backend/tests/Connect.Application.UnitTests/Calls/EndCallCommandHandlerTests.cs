using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.EndCall;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class EndCallCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly EndCallCommandHandler _handler;

    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _calleeId = Guid.NewGuid();
    private readonly Guid _callId = Guid.NewGuid();

    public EndCallCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(_callerId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new EndCallCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _presenceTrackerMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_CallNotFound_ThrowsNotFoundException()
    {
        _callRepoMock.Setup(r => r.GetByIdAsync(_callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Call?)null);

        var command = new EndCallCommand(_callId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotParticipant_ThrowsForbiddenAccessException()
    {
        var call = new Call { Id = _callId, CallerId = Guid.NewGuid(), CalleeId = Guid.NewGuid() };
        _callRepoMock.Setup(r => r.GetByIdAsync(_callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var command = new EndCallCommand(_callId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidParticipant_EndsCallCalculatesDurationAndResetsPresence()
    {
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        var answerTime = startTime.AddSeconds(10);
        var now = DateTime.UtcNow;

        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(now);

        var call = new Call
        {
            Id = _callId,
            CallerId = _callerId,
            CalleeId = _calleeId,
            Status = CallStatus.Accepted,
            StartedAt = startTime,
            AnsweredAt = answerTime
        };

        _callRepoMock.Setup(r => r.GetByIdAsync(_callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var caller = new User { Id = _callerId, PresenceStatus = PresenceStatus.Busy };
        var callee = new User { Id = _calleeId, PresenceStatus = PresenceStatus.Busy };

        _userRepoMock.Setup(r => r.GetByIdAsync(_callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepoMock.Setup(r => r.GetByIdAsync(_calleeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(callee);

        var command = new EndCallCommand(_callId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_callId, result.CallId);
        Assert.Equal(_calleeId, result.OtherUserId);
        Assert.Equal((int)(now - answerTime).TotalSeconds, result.DurationSeconds);
        Assert.Equal(CallStatus.Completed, call.Status);

        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_callerId, PresenceStatus.Online), Times.Once);
        _presenceTrackerMock.Verify(p => p.SetUserPresenceAsync(_calleeId, PresenceStatus.Online), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
