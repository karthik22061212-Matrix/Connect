using Connect.Application.Features.Blocking.Commands.BlockUser;
using Connect.Application.Features.Blocking.Commands.UnblockUser;
using Connect.Application.Features.Blocking.Models;
using Connect.Application.Features.Blocking.Queries.GetBlockedUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class BlockingController : ControllerBase
{
    private readonly ISender _mediator;

    public BlockingController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{userId}/block")]
    public async Task<IActionResult> BlockUser(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new BlockUserCommand(userId), ct);
        return Ok();
    }

    [HttpDelete("{userId}/block")]
    public async Task<IActionResult> UnblockUser(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new UnblockUserCommand(userId), ct);
        return Ok();
    }

    [HttpGet("blocked")]
    public async Task<ActionResult<IReadOnlyList<BlockedUserDto>>> GetBlockedUsers(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBlockedUsersQuery(), ct);
        return Ok(result);
    }
}
