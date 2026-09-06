using System;
using Connect.Application.Common.Interfaces;
using Connect.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/turn")]
public class TurnController : ControllerBase
{
    private readonly ITurnCredentialService _turnCredentialService;
    private readonly ICurrentUserService _currentUserService;

    public TurnController(ITurnCredentialService turnCredentialService, ICurrentUserService currentUserService)
    {
        _turnCredentialService = turnCredentialService;
        _currentUserService = currentUserService;
    }

    [HttpGet("credentials")]
    public ActionResult<TurnCredentialsDto> GetCredentials()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var credentials = _turnCredentialService.GenerateCredentials(userId.Value.ToString());
            return Ok(credentials);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
