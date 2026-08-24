using Connect.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestErrorController : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound()
    {
        throw new NotFoundException("Test resource was not found.");
    }

    [HttpGet("conflict")]
    public IActionResult ThrowConflict()
    {
        throw new ConflictException("Test User ID already taken.");
    }

    [HttpGet("forbidden")]
    public IActionResult ThrowForbidden()
    {
        throw new ForbiddenAccessException("Cannot call a non-connected user.");
    }

    [HttpGet("server-error")]
    public IActionResult ThrowServerError()
    {
        throw new InvalidOperationException("Simulated unexpected database failure.");
    }
}
