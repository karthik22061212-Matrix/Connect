using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using Connect.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Connect.Application.UnitTests.Services; // for ApplicationDbContextMock

namespace Connect.Application.UnitTests.Calls;

public class CallHubPresenceTests : IDisposable
{
    private readonly ApplicationDbContextMock _context;
    private readonly PresenceVisibilityService _presenceVisibilityService;
    
    private readonly Mock<IPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ISender> _mediatorMock = new();
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new();
    private readonly Mock<ILogger<CallHub>> _loggerMock = new();
    private readonly Mock<Connect.Application.Common.Diagnostics.IDiagnosticLogService> _diagnosticLogServiceMock = new();
    private readonly Mock<IHubCallerClients<ICallHubClient>> _clientsMock = new();
    private readonly Mock<ICallHubClient> _clientProxyMock = new();
    private readonly Mock<HubCallerContext> _contextMock = new();

    private readonly Guid _ownerId = Guid.NewGuid();

    public CallHubPresenceTests()
    {
        _context = new ApplicationDbContextMock();
        _presenceVisibilityService = new PresenceVisibilityService(_context);
        
        var usersMock = new Mock<IRepository<User>>();
        _unitOfWorkMock.Setup(u => u.Users).Returns(usersMock.Object);

        _clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_clientProxyMock.Object);
        _clientsMock.Setup(c => c.Caller).Returns(_clientProxyMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private CallHub CreateHub()
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, _ownerId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);
        _contextMock.Setup(c => c.UserIdentifier).Returns(_ownerId.ToString());
        _contextMock.Setup(c => c.ConnectionId).Returns("conn-owner");

        return new CallHub(
            _presenceTrackerMock.Object,
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object,
            _mediatorMock.Object,
            _serviceScopeFactoryMock.Object,
            _loggerMock.Object,
            _diagnosticLogServiceMock.Object,
            _presenceVisibilityService)
        {
            Context = _contextMock.Object,
            Clients = _clientsMock.Object
        };
    }

    private void SetupPotentialViewers(List<Guid> viewerIds)
    {
        _presenceTrackerMock.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(viewerIds);
        foreach (var v in viewerIds)
        {
            _presenceTrackerMock.Setup(p => p.GetConnectionIdsForUserAsync(v)).ReturnsAsync(new List<string> { $"conn-{v}" });
        }
    }

    [Fact]
    public async Task UpdatePresence_AuthorizedViewerReceivesMessage_UnauthorizedDoesNot()
    {
        var authorizedViewer = Guid.NewGuid();
        var unauthorizedViewer = Guid.NewGuid();

        // Defaults to ConnectionsOnly, so we connect authorizedViewer
        _context.Connections.Add(new Connection { UserAId = _ownerId, UserBId = authorizedViewer });
        await _context.SaveChangesAsync();

        SetupPotentialViewers(new List<Guid> { authorizedViewer, unauthorizedViewer });
        var hub = CreateHub();

        await hub.UpdatePresence(PresenceStatus.Online);

        _clientsMock.Verify(c => c.Clients(It.Is<IReadOnlyList<string>>(list => list.Contains($"conn-{authorizedViewer}"))), Times.Once);
        _clientsMock.Verify(c => c.Clients(It.Is<IReadOnlyList<string>>(list => list.Contains($"conn-{unauthorizedViewer}"))), Times.Never);
        _clientProxyMock.Verify(c => c.UserPresenceChanged(_ownerId, PresenceStatus.Online), Times.Once);
    }

    [Fact]
    public async Task UpdatePresence_BlockedViewer_DoesNotReceiveMessage()
    {
        var blockedViewer = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = _ownerId, PresenceVisibility = PresenceVisibility.Everyone });
        _context.Blocks.Add(new Block { BlockerUserId = _ownerId, BlockedUserId = blockedViewer });
        await _context.SaveChangesAsync();

        SetupPotentialViewers(new List<Guid> { blockedViewer });
        var hub = CreateHub();

        await hub.UpdatePresence(PresenceStatus.Online);

        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePresence_Nobody_OtherViewerReceivesNoPresence()
    {
        var viewer = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = _ownerId, PresenceVisibility = PresenceVisibility.Nobody });
        await _context.SaveChangesAsync();

        SetupPotentialViewers(new List<Guid> { viewer });
        var hub = CreateHub();

        await hub.UpdatePresence(PresenceStatus.Online);

        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePresence_CustomAllowed_ReceivesPresence()
    {
        var viewer = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = _ownerId, PresenceVisibility = PresenceVisibility.Custom });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = _ownerId, TargetUserId = viewer, IsAllowed = true });
        await _context.SaveChangesAsync();

        SetupPotentialViewers(new List<Guid> { viewer });
        var hub = CreateHub();

        await hub.UpdatePresence(PresenceStatus.Online);

        _clientsMock.Verify(c => c.Clients(It.Is<IReadOnlyList<string>>(list => list.Contains($"conn-{viewer}"))), Times.Once);
    }

    [Fact]
    public async Task UpdatePresence_CustomDenied_DoesNotReceivePresence()
    {
        var viewer = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = _ownerId, PresenceVisibility = PresenceVisibility.Custom });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = _ownerId, TargetUserId = viewer, IsAllowed = false });
        await _context.SaveChangesAsync();

        SetupPotentialViewers(new List<Guid> { viewer });
        var hub = CreateHub();

        await hub.UpdatePresence(PresenceStatus.Online);

        _clientsMock.Verify(c => c.Clients(It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }
}
