using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.PresenceSettings.Queries.GetPresenceSettings;

public class GetPresenceSettingsQueryHandler : IRequestHandler<GetPresenceSettingsQuery, PresenceVisibility>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPresenceSettingsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PresenceVisibility> Handle(GetPresenceSettingsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var setting = await _context.UserPresenceSettings
            .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

        return setting?.PresenceVisibility ?? PresenceVisibility.ConnectionsOnly;
    }
}
