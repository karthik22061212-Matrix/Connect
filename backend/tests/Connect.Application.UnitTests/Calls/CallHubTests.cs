using System.Security.Claims;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.InitiateCall;
using Connect.Application.Features.Calls.Commands.RecordNetworkDrop;
using Connect.Application.Features.Calls.Commands.RecordNetworkRestored;
using Connect.Application.Features.Calls.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class CallHubTests
{
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ISender> _mediatorMock = new();
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new();
    private readonly Mock<ILogger<CallHub>> _loggerMock = new();
    private readonly Mock<IHubCallerClients<ICallHubClient>> _clientsMock = new();
    private readonly Mock<ICallHubClient> _callerClientMock = new();
    private readonly Mock<ICallHubClient> _targetClientMock = new();
    private readonly Mock<HubCallerContext> _contextMock = new();

    private readonly CallHub _hub;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _calleeId = Guid.NewGuid();
    private readonly Guid _callId = Guid.NewGuid();

    public CallHubTests()
    {
        _clientsMock.Setup(c => c.Caller).Returns(_callerClientMock.Object);
        _clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_targetClientMock.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _contextMock.Setup(c => c.ConnectionId).Returns("conn-caller");

        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<string> { "conn-callee" });

        _hub = new CallHub(
            _presenceTrackerMock.Object,
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object,
            _mediatorMock.Object,
            _serviceScopeFactoryMock.Object)
        {
            Context = _contextMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task InitiateCallAttempt_RingingResult_NotifiesCalleeAndDoesNotSpawnInProcessTimer()
    {
        // Arrange
        var result = new CallResultDto(_callId, _userId, _calleeId, CallStatus.Ringing, null, "callerHandle");
        _mediatorMock.Setup(m => m.Send(It.IsAny<InitiateCallCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        await _hub.InitiateCallAttempt(_calleeId);

        // Assert
        _mediatorMock.Verify(m => m.Send(It.Is<InitiateCallCommand>(c => c.CalleeId == _calleeId), It.IsAny<CancellationToken>()), Times.Once);
        _targetClientMock.Verify(c => c.IncomingCall(_callId, _userId, "callerHandle"), Times.Once);
    }

    [Fact]
    public async Task NotifyNetworkDrop_SendsCommandAndNotifiesOtherUser()
    {
        // Arrange
        var dropResult = new RecordNetworkDropResultDto(_callId, _userId, _calleeId, _calleeId);
        _mediatorMock.Setup(m => m.Send(It.IsAny<RecordNetworkDropCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dropResult);

        // Act
        await _hub.NotifyNetworkDrop(_callId);

        // Assert
        _mediatorMock.Verify(m => m.Send(It.Is<RecordNetworkDropCommand>(c => c.CallId == _callId), It.IsAny<CancellationToken>()), Times.Once);
        _targetClientMock.Verify(c => c.NetworkReconnecting(_callId), Times.Once);
    }

    [Fact]
    public async Task NotifyNetworkRestored_SendsCommandAndNotifiesOtherUser()
    {
        // Arrange
        var restoredResult = new RecordNetworkRestoredResultDto(_callId, _userId, _calleeId, _calleeId);
        _mediatorMock.Setup(m => m.Send(It.IsAny<RecordNetworkRestoredCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(restoredResult);

        // Act
        await _hub.NotifyNetworkRestored(_callId);

        // Assert
        _mediatorMock.Verify(m => m.Send(It.Is<RecordNetworkRestoredCommand>(c => c.CallId == _callId), It.IsAny<CancellationToken>()), Times.Once);
        _targetClientMock.Verify(c => c.NetworkRestored(_callId), Times.Once);
    }
}
