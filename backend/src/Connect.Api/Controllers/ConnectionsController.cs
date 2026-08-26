using Connect.Application.Features.Connections.Models;
using Connect.Application.Features.Connections.Queries.GetConnections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly ISender _mediator;

    public ConnectionsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConnectionDto>>> GetConnections(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetConnectionsQuery(), ct);
        return Ok(result);
    }
}
