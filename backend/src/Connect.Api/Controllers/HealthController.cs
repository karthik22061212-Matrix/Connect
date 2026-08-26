using Connect.Application.Features.HealthCheck.Queries.GetHealthCheck;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IMediator _mediator;

    public HealthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<HealthCheckDto>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHealthCheckQuery(), ct);
        return Ok(result);
    }
}
