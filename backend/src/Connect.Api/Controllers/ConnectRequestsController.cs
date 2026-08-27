using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;
using Connect.Application.Features.ConnectRequests.Commands.DeclineConnectRequest;
using Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Application.Features.ConnectRequests.Queries.GetPendingRequests;
using Connect.Infrastructure.Realtime;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/connect-requests")]
public class ConnectRequestsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IHubContext<CallHub, ICallHubClient> _hubContext;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IUnitOfWork _unitOfWork;

    public ConnectRequestsController(
        ISender mediator,
        IHubContext<CallHub, ICallHubClient> hubContext,
        IPresenceTracker presenceTracker,
        IUnitOfWork unitOfWork)
    {
        _mediator = mediator;
        _hubContext = hubContext;
        _presenceTracker = presenceTracker;
        _unitOfWork = unitOfWork;
    }

    [HttpPost]
    public async Task<ActionResult<ConnectRequestDto>> SendConnectRequest([FromBody] SendConnectRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        try
        {
            var sender = await _unitOfWork.Users.GetByIdAsync(result.FromUserId, ct);
            var targetConns = await _presenceTracker.GetConnectionIdsForUserAsync(result.ToUserId);
            if (targetConns.Count > 0)
            {
                await _hubContext.Clients.Clients(targetConns).ConnectRequestReceived(
                    result.Id,
                    result.FromUserId,
                    sender?.UserId ?? ""
                );
            }
        }
        catch
        {
            // SignalR notification is best-effort
        }

        return Ok(result);
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult<ConnectRequestDto>> AcceptConnectRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AcceptConnectRequestCommand(id), ct);

        try
        {
            var acceptingUser = await _unitOfWork.Users.GetByIdAsync(result.ToUserId, ct);
            var senderConns = await _presenceTracker.GetConnectionIdsForUserAsync(result.FromUserId);
            if (senderConns.Count > 0)
            {
                await _hubContext.Clients.Clients(senderConns).ConnectRequestAccepted(
                    result.Id,
                    result.ToUserId,
                    acceptingUser?.UserId ?? ""
                );
            }
        }
        catch
        {
            // SignalR notification is best-effort
        }

        return Ok(result);
    }

    [HttpPost("{id}/decline")]
    public async Task<ActionResult<ConnectRequestDto>> DeclineConnectRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeclineConnectRequestCommand(id), ct);

        try
        {
            var senderConns = await _presenceTracker.GetConnectionIdsForUserAsync(result.FromUserId);
            if (senderConns.Count > 0)
            {
                await _hubContext.Clients.Clients(senderConns).ConnectRequestDeclined(
                    result.Id,
                    result.ToUserId
                );
            }
        }
        catch
        {
            // SignalR notification is best-effort
        }

        return Ok(result);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<PendingConnectRequestDto>>> GetPendingRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingRequestsQuery(), ct);
        return Ok(result);
    }
}
