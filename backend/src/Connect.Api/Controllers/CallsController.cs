using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.EndCall;
using Connect.Application.Features.Calls.Models;
using Connect.Infrastructure.Realtime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/calls")]
public class CallsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<CallHub, ICallHubClient> _hubContext;
    private readonly IPresenceTracker _presenceTracker;

    public CallsController(
        ISender mediator,
        IHubContext<CallHub, ICallHubClient> hubContext,
        IPresenceTracker presenceTracker)
    {
        _mediator = mediator;
        _hubContext = hubContext;
        _presenceTracker = presenceTracker;
    }

    [HttpPost("{callId}/end")]
    public async Task<ActionResult<EndCallResultDto>> EndCall(Guid callId, CancellationToken ct)
    {
        var result = await _mediator.Send(new EndCallCommand(callId), ct);

        var otherConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.OtherUserId);
        if (otherConnections.Count > 0)
        {
            await _hubContext.Clients.Clients(otherConnections).CallEnded(callId);
        }

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<Connect.Application.Common.Models.PaginatedList<CallHistoryDto>>> GetCallHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Connect.Application.Features.Calls.Queries.GetCallHistory.GetCallHistoryQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }
}


