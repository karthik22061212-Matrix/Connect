using Connect.Application.Features.Presence.Commands.UpdatePresence;
using Connect.Application.Features.Presence.Models;
using Connect.Application.Features.Presence.Queries.GetPresence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/presence")]
public class PresenceController : ControllerBase
{
    private readonly ISender _mediator;

    public PresenceController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPut]
    public async Task<ActionResult<PresenceDto>> UpdatePresence([FromBody] UpdatePresenceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<PresenceDto>> GetPresence(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPresenceQuery(userId), ct);
        return Ok(result);
    }
}
