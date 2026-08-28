using System.Security.Claims;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Infrastructure.Realtime;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.Calls;

public class CallHubTests
{
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepositoryMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ISender> _mediatorMock = new();
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new();
    private readonly Mock<ILogger<CallHub>> _loggerMock = new();
    private readonly Mock<IHubCallerClients<ICallHubClient>> _clientsMock = new();
    private readonly Mock<ICallHubClient> _clientProxyMock = new();
    private readonly Mock<HubCallerContext> _contextMock = new();

    public CallHubTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepositoryMock.Object);
        _clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_clientProxyMock.Object);
    }

    private CallHub CreateHubWithAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _contextMock.Setup(c => c.UserIdentifier).Returns(userId.ToString());

        return new CallHub(
            _presenceTrackerMock.Object,
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object,
            _mediatorMock.Object,
            _serviceScopeFactoryMock.Object,
            _loggerMock.Object)
        {
            Context = _contextMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task SendWebRtcOffer_WhenUserIsNotParticipant_ThrowsHubExceptionAndDoesNotForwardMessage()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();
        var unauthenticatedUser = Guid.NewGuid();
        var callId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId
        };

        _callRepositoryMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var hub = CreateHubWithAuthenticatedUser(unauthenticatedUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() => hub.SendWebRtcOffer(callId, "test-sdp"));
        Assert.Equal("Unauthorized call access.", exception.Message);

        _presenceTrackerMock.Verify(p => p.GetConnectionIdsForUserAsync(It.IsAny<Guid>()), Times.Never);
        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        _clientProxyMock.Verify(c => c.ReceiveWebRtcOffer(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendWebRtcAnswer_WhenUserIsNotParticipant_ThrowsHubExceptionAndDoesNotForwardMessage()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();
        var unauthenticatedUser = Guid.NewGuid();
        var callId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId
        };

        _callRepositoryMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var hub = CreateHubWithAuthenticatedUser(unauthenticatedUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() => hub.SendWebRtcAnswer(callId, "test-sdp"));
        Assert.Equal("Unauthorized call access.", exception.Message);

        _presenceTrackerMock.Verify(p => p.GetConnectionIdsForUserAsync(It.IsAny<Guid>()), Times.Never);
        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        _clientProxyMock.Verify(c => c.ReceiveWebRtcAnswer(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendIceCandidate_WhenUserIsNotParticipant_ThrowsHubExceptionAndDoesNotForwardMessage()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();
        var unauthenticatedUser = Guid.NewGuid();
        var callId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId
        };

        _callRepositoryMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var hub = CreateHubWithAuthenticatedUser(unauthenticatedUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() => hub.SendIceCandidate(callId, "test-candidate"));
        Assert.Equal("Unauthorized call access.", exception.Message);

        _presenceTrackerMock.Verify(p => p.GetConnectionIdsForUserAsync(It.IsAny<Guid>()), Times.Never);
        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        _clientProxyMock.Verify(c => c.ReceiveIceCandidate(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendWebRtcOffer_WhenUserIsCaller_ForwardsOfferToCallee()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId
        };

        _callRepositoryMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var connectionIds = new List<string> { "conn-callee-1" };
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(calleeId))
            .ReturnsAsync(connectionIds);

        var hub = CreateHubWithAuthenticatedUser(callerId);

        // Act
        await hub.SendWebRtcOffer(callId, "test-sdp");

        // Assert
        _presenceTrackerMock.Verify(p => p.GetConnectionIdsForUserAsync(calleeId), Times.Once);
        _clientsMock.Verify(c => c.Clients(connectionIds), Times.Once);
        _clientProxyMock.Verify(c => c.ReceiveWebRtcOffer(callId, "test-sdp"), Times.Once);
    }

    [Fact]
    public async Task SendWebRtcAnswer_WhenUserIsCallee_ForwardsAnswerToCaller()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var calleeId = Guid.NewGuid();
        var callId = Guid.NewGuid();

        var call = new Call
        {
            Id = callId,
            CallerId = callerId,
            CalleeId = calleeId
        };

        _callRepositoryMock.Setup(r => r.GetByIdAsync(callId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(call);

        var connectionIds = new List<string> { "conn-caller-1" };
        _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(callerId))
            .ReturnsAsync(connectionIds);

        var hub = CreateHubWithAuthenticatedUser(calleeId);

        // Act
        await hub.SendWebRtcAnswer(callId, "test-sdp");

        // Assert
        _presenceTrackerMock.Verify(p => p.GetConnectionIdsForUserAsync(callerId), Times.Once);
        _clientsMock.Verify(c => c.Clients(connectionIds), Times.Once);
        _clientProxyMock.Verify(c => c.ReceiveWebRtcAnswer(callId, "test-sdp"), Times.Once);
    }
}
