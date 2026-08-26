using Connect.Application.Features.Account.Commands.SoftDeleteAccount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/account")]
public class AccountController : ControllerBase
{
    private readonly ISender _mediator;

    public AccountController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpDelete]
    public async Task<IActionResult> SoftDeleteAccount(CancellationToken ct)
    {
        await _mediator.Send(new SoftDeleteAccountCommand(), ct);
        return NoContent();
    }
}
