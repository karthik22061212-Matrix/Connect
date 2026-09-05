using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Connect.Application.Common.Interfaces;

public interface IPresenceVisibilityService
{
    Task<bool> CanViewPresenceAsync(
        Guid ownerUserId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetAuthorizedViewersAsync(
        Guid ownerUserId,
        IEnumerable<Guid> potentialViewerIds,
        CancellationToken cancellationToken = default);
}
