using Connect.Application.Features.Connections.Commands.DisconnectConnection;
using Connect.Application.Features.Connections.Models;
using Connect.Application.Features.Connections.Queries.GetConnections;
using Connect.Infrastructure.Realtime;
using Connect.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<CallHub, ICallHubClient> _hubContext;
    private readonly IPresenceTracker _presenceTracker;

    public ConnectionsController(
        ISender mediator,
        IHubContext<CallHub, ICallHubClient> hubContext,
        IPresenceTracker presenceTracker)
    {
        _mediator = mediator;
        _hubContext = hubContext;
        _presenceTracker = presenceTracker;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConnectionDto>>> GetConnections(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetConnectionsQuery(), ct);
        return Ok(result);
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DisconnectConnection(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new DisconnectConnectionCommand(userId), ct);

        try
        {
            var currentUserStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name;

            if (Guid.TryParse(currentUserStr, out var currentUserId))
            {
                var targetConns = await _presenceTracker.GetConnectionIdsForUserAsync(userId);
                if (targetConns.Count > 0)
                {
                    await _hubContext.Clients.Clients(targetConns).ConnectionRemoved(currentUserId);
                }

                var senderConns = await _presenceTracker.GetConnectionIdsForUserAsync(currentUserId);
                if (senderConns.Count > 0)
                {
                    await _hubContext.Clients.Clients(senderConns).ConnectionRemoved(userId);
                }
            }
        }
        catch
        {
            // SignalR notification is best-effort
        }

        return NoContent();
    }
}
