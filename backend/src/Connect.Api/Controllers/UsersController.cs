using Connect.Application.Features.Users.Models;
using Connect.Application.Features.Users.Queries.CheckUserIdAvailability;
using Connect.Application.Features.Users.Queries.SearchUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ISender _mediator;

    public UsersController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("check-userid")]
    [AllowAnonymous]
    public async Task<ActionResult<UserIdAvailabilityDto>> CheckUserIdAvailability([FromQuery] string value, CancellationToken ct)
    {
        var result = await _mediator.Send(new CheckUserIdAvailabilityQuery(value), ct);
        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UserSearchResultDto>>> SearchUsers([FromQuery] string query, CancellationToken ct)
    {
        var result = await _mediator.Send(new SearchUsersQuery(query), ct);
        return Ok(result);
    }
}
