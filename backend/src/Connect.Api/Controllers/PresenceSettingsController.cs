using System;
using System.Threading.Tasks;
using Connect.Application.Features.PresenceSettings.Commands.DeletePresenceVisibilityException;
using Connect.Application.Features.PresenceSettings.Commands.SetPresenceVisibilityException;
using Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;
using Connect.Application.Features.PresenceSettings.Queries.GetPresenceSettings;
using Connect.Application.Features.PresenceSettings.Queries.GetPresenceVisibilityExceptions;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/presence/settings")]
public class PresenceSettingsController : ControllerBase
{
    private readonly ISender _mediator;

    public PresenceSettingsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _mediator.Send(new GetPresenceSettingsQuery());
        return Ok(new { Visibility = result });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePresenceSettingsRequest request)
    {
        await _mediator.Send(new UpdatePresenceSettingsCommand(request.Visibility));
        return NoContent();
    }

    [HttpGet("exceptions")]
    public async Task<IActionResult> GetExceptions()
    {
        var result = await _mediator.Send(new GetPresenceVisibilityExceptionsQuery());
        return Ok(result);
    }

    [HttpPost("exceptions")]
    public async Task<IActionResult> SetException([FromBody] SetPresenceVisibilityExceptionRequest request)
    {
        await _mediator.Send(new SetPresenceVisibilityExceptionCommand(request.TargetUserId, request.IsAllowed));
        return NoContent();
    }

    [HttpDelete("exceptions/{targetUserId:guid}")]
    public async Task<IActionResult> DeleteException(Guid targetUserId)
    {
        await _mediator.Send(new DeletePresenceVisibilityExceptionCommand(targetUserId));
        return NoContent();
    }
}

public class UpdatePresenceSettingsRequest
{
    public PresenceVisibility Visibility { get; set; }
}

public class SetPresenceVisibilityExceptionRequest
{
    public Guid TargetUserId { get; set; }
    public bool IsAllowed { get; set; }
}
