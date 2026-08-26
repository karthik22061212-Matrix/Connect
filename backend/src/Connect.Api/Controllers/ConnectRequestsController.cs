using Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;
using Connect.Application.Features.ConnectRequests.Commands.DeclineConnectRequest;
using Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Application.Features.ConnectRequests.Queries.GetPendingRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/connect-requests")]
public class ConnectRequestsController : ControllerBase
{
    private readonly ISender _mediator;

    public ConnectRequestsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<ConnectRequestDto>> SendConnectRequest([FromBody] SendConnectRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult<ConnectRequestDto>> AcceptConnectRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AcceptConnectRequestCommand(id), ct);
        return Ok(result);
    }

    [HttpPost("{id}/decline")]
    public async Task<ActionResult<ConnectRequestDto>> DeclineConnectRequest(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeclineConnectRequestCommand(id), ct);
        return Ok(result);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<PendingConnectRequestDto>>> GetPendingRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingRequestsQuery(), ct);
        return Ok(result);
    }
}
