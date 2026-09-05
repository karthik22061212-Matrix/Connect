using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Connect.Infrastructure.Services;

public class PresenceVisibilityService : IPresenceVisibilityService
{
    private readonly IApplicationDbContext _context;

    public PresenceVisibilityService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanViewPresenceAsync(Guid ownerUserId, Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        var authorizedViewers = await GetAuthorizedViewersAsync(ownerUserId, new[] { viewerUserId }, cancellationToken);
        return authorizedViewers.Contains(viewerUserId);
    }

    public async Task<IReadOnlyCollection<Guid>> GetAuthorizedViewersAsync(Guid ownerUserId, IEnumerable<Guid> potentialViewerIds, CancellationToken cancellationToken = default)
    {
        var potentialViewersList = potentialViewerIds.Distinct().ToList();
        if (!potentialViewersList.Any())
            return new List<Guid>();

        // Always allow self
        var result = new HashSet<Guid>();
        if (potentialViewersList.Contains(ownerUserId))
        {
            result.Add(ownerUserId);
        }

        var setting = await _context.UserPresenceSettings
            .FirstOrDefaultAsync(s => s.UserId == ownerUserId, cancellationToken);

        var visibility = setting?.PresenceVisibility ?? PresenceVisibility.ConnectionsOnly;

        if (visibility == PresenceVisibility.Nobody)
        {
            return result;
        }

        // Exclude blocked users from potential viewers
        var blocks = await _context.Blocks
            .Where(b => b.BlockerUserId == ownerUserId || b.BlockedUserId == ownerUserId)
            .ToListAsync(cancellationToken);

        var blockedUserIds = blocks
            .Select(b => b.BlockerUserId == ownerUserId ? b.BlockedUserId : b.BlockerUserId)
            .ToHashSet();

        var eligibleViewers = potentialViewersList
            .Where(id => id != ownerUserId && !blockedUserIds.Contains(id))
            .ToList();

        if (!eligibleViewers.Any())
            return result;

        switch (visibility)
        {
            case PresenceVisibility.Everyone:
                foreach (var id in eligibleViewers) result.Add(id);
                break;

            case PresenceVisibility.ConnectionsOnly:
                var connections = await _context.Connections
                    .Where(c => c.UserAId == ownerUserId || c.UserBId == ownerUserId)
                    .ToListAsync(cancellationToken);

                var connectedUserIds = connections
                    .Select(c => c.UserAId == ownerUserId ? c.UserBId : c.UserAId)
                    .ToHashSet();

                foreach (var id in eligibleViewers.Where(id => connectedUserIds.Contains(id)))
                {
                    result.Add(id);
                }
                break;

            case PresenceVisibility.Custom:
                var exceptions = await _context.PresenceVisibilityExceptions
                    .Where(e => e.OwnerUserId == ownerUserId && e.IsAllowed)
                    .Select(e => e.TargetUserId)
                    .ToListAsync(cancellationToken);

                var allowedUserIds = exceptions.ToHashSet();

                foreach (var id in eligibleViewers.Where(id => allowedUserIds.Contains(id)))
                {
                    result.Add(id);
                }
                break;
        }

        return result.ToList();
    }
}
