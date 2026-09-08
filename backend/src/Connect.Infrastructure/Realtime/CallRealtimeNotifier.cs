using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Infrastructure.Realtime;

public class CallRealtimeNotifier : ICallRealtimeNotifier
{
    private readonly IHubContext<CallHub, ICallHubClient> _hubContext;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CallRealtimeNotifier(
        IHubContext<CallHub, ICallHubClient> hubContext,
        IPresenceTracker presenceTracker,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _hubContext = hubContext;
        _presenceTracker = presenceTracker;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task TerminateActiveCallsBetweenUsersAsync(Guid userAId, Guid userBId, string reason, CancellationToken ct)
    {
        var activeCalls = await _unitOfWork.Calls.ListAsync(ct);
        var targetCalls = activeCalls.Where(c =>
            (c.Status == CallStatus.Ringing || c.Status == CallStatus.Accepted) &&
            ((c.CallerId == userAId && c.CalleeId == userBId) || (c.CallerId == userBId && c.CalleeId == userAId))
        ).ToList();

        if (targetCalls.Count == 0)
        {
            return;
        }

        foreach (var call in targetCalls)
        {
            call.Status = CallStatus.Failed;
            call.EndedAt = _dateTimeProvider.UtcNow;
            call.UpdatedAt = _dateTimeProvider.UtcNow;
            call.TimeoutDeadline = null;
            call.TimeoutType = null;
        }

        await _presenceTracker.SetUserPresenceAsync(userAId, PresenceStatus.Online);
        await _presenceTracker.SetUserPresenceAsync(userBId, PresenceStatus.Online);

        if (_unitOfWork.Users != null)
        {
            var userA = await _unitOfWork.Users.GetByIdAsync(userAId, ct);
            if (userA != null && !userA.IsDeleted)
            {
                userA.PresenceStatus = PresenceStatus.Online;
                userA.UpdatedAt = _dateTimeProvider.UtcNow;
            }

            var userB = await _unitOfWork.Users.GetByIdAsync(userBId, ct);
            if (userB != null && !userB.IsDeleted)
            {
                userB.PresenceStatus = PresenceStatus.Online;
                userB.UpdatedAt = _dateTimeProvider.UtcNow;
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Concurrent call state modification already committed; proceed with notification
        }

        var userAConnections = await _presenceTracker.GetConnectionIdsForUserAsync(userAId);
        var userBConnections = await _presenceTracker.GetConnectionIdsForUserAsync(userBId);
        var allConnections = userAConnections.Concat(userBConnections).Distinct().ToList();

        if (allConnections.Count > 0)
        {
            foreach (var call in targetCalls)
            {
                try
                {
                    await _hubContext.Clients.Clients(allConnections).CallEnded(call.Id);
                }
                catch
                {
                    // SignalR teardown notification is best-effort
                }
            }
        }
    }

    public async Task NotifyConnectionRemovedAsync(Guid userAId, Guid userBId, CancellationToken ct)
    {
        try
        {
            var userAConnections = await _presenceTracker.GetConnectionIdsForUserAsync(userAId);
            var userBConnections = await _presenceTracker.GetConnectionIdsForUserAsync(userBId);

            if (userAConnections.Count > 0)
                await _hubContext.Clients.Clients(userAConnections).ConnectionRemoved(userBId);
            if (userBConnections.Count > 0)
                await _hubContext.Clients.Clients(userBConnections).ConnectionRemoved(userAId);
        }
        catch
        {
            // SignalR notification is best-effort
        }
    }
}
