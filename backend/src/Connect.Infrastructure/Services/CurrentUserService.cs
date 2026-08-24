using System.Security.Claims;
using Connect.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Connect.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdStr, out var id) ? id : null;
        }
    }

    public string? UserHandle => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
}
