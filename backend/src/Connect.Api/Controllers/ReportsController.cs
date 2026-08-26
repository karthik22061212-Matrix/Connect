using Connect.Application.Features.Reports.Commands.ReportUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public class ReportsController : ControllerBase
{
    private readonly ISender _mediator;

    public ReportsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> ReportUser([FromBody] ReportUserCommand command, CancellationToken ct)
    {
        var reportId = await _mediator.Send(command, ct);
        return Ok(reportId);
    }
}
