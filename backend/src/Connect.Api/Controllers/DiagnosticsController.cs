using System.Security.Claims;
using Connect.Application.Common.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DiagnosticsController : ControllerBase
{
    private readonly IDiagnosticLogService _diagnosticLogService;

    public DiagnosticsController(IDiagnosticLogService diagnosticLogService)
    {
        _diagnosticLogService = diagnosticLogService;
    }

    [HttpPost("client-logs")]
    public IActionResult IngestClientLogs([FromBody] List<DiagnosticEvent> logs)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (logs == null || logs.Count == 0) return Ok();

        // Enforce maximum batch size for safety
        if (logs.Count > 200)
        {
            logs = logs.Take(200).ToList();
        }

        _diagnosticLogService.IngestClientLogs(userId, logs);
        return Ok();
    }
}
