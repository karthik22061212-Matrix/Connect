using Connect.Application.Common.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Route("api/v1/admin/diagnostics")]
[Authorize]
public class AdminDiagnosticsController : ControllerBase
{
    private readonly IDiagnosticLogService _diagnosticLogService;

    public AdminDiagnosticsController(IDiagnosticLogService diagnosticLogService)
    {
        _diagnosticLogService = diagnosticLogService;
    }

    [HttpGet("{userId}/download")]
    public IActionResult DownloadDiagnostics(string userId)
    {
        var logs = _diagnosticLogService.GetCombinedLogs(userId);
        return Ok(logs);
    }

    [HttpPost("{userId}/clear")]
    public IActionResult ClearDiagnostics(string userId)
    {
        _diagnosticLogService.ClearLogs(userId);
        return Ok();
    }
}
