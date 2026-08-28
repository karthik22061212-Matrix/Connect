using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Services;

public class CallTimeoutProcessor : ICallTimeoutProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IHubContext<CallHub, ICallHubClient> _hubContext;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<CallTimeoutProcessor> _logger;

    public CallTimeoutProcessor(
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IPresenceTracker presenceTracker,
        IHubContext<CallHub, ICallHubClient> hubContext,
        IPushNotificationService pushNotificationService,
        ILogger<CallTimeoutProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _presenceTracker = presenceTracker;
        _hubContext = hubContext;
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    public async Task ProcessExpiredTimeoutsAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        var dueCalls = await _unitOfWork.Calls.ListAsync(
            c => c.TimeoutDeadline != null && c.TimeoutDeadline <= now,
            cancellationToken);

        foreach (var call in dueCalls)
        {
            if (call.TimeoutType == CallTimeoutType.Ring)
            {
                // Idempotency Guard: transition only if call is still in Ringing status
                if (call.Status == CallStatus.Ringing)
                {
                    call.Status = CallStatus.Missed;
                    call.MissedReason = MissedReason.NoAnswer;
                    call.EndedAt = now;
                    call.UpdatedAt = now;
                    call.TimeoutDeadline = null;
                    call.TimeoutType = null;

                    try
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        _logger.LogWarning(ex, "Concurrency conflict processing Ring timeout for Call {CallId}. Reloading entity.", call.Id);
                        var entry = ex.Entries.FirstOrDefault();
                        if (entry != null)
                        {
                            await entry.ReloadAsync(cancellationToken);
                        }

                        if (call.Status != CallStatus.Ringing)
                        {
                            _logger.LogInformation("Call {CallId} status changed to {Status} concurrently; Ring timeout is moot.", call.Id, call.Status);
                            continue;
                        }
                    }

                    var callerConn = await _presenceTracker.GetConnectionIdsForUserAsync(call.CallerId);
                    var calleeConn = await _presenceTracker.GetConnectionIdsForUserAsync(call.CalleeId);

                    await _hubContext.Clients.Clients(callerConn).CallTimeout(call.Id);
                    await _hubContext.Clients.Clients(callerConn).CalleeUnavailable(call.CalleeId, "NoAnswer");
                    await _hubContext.Clients.Clients(calleeConn).CallEnded(call.Id);

                    var callerUser = await _unitOfWork.Users.GetByIdAsync(call.CallerId, cancellationToken);
                    if (calleeConn.Count > 0)
                    {
                        await _hubContext.Clients.Clients(calleeConn).MissedCallNotification(
                            call.Id, call.CallerId, callerUser?.UserId ?? "", call.StartedAt);
                    }

                    await _pushNotificationService.SendMissedCallNotificationAsync(
                        call.CalleeId, call.Id, callerUser?.UserId ?? "", MissedReason.NoAnswer, cancellationToken);

                    _logger.LogInformation("Processed Ring timeout for Call {CallId}. Marked as Missed (NoAnswer).", call.Id);
                }
                else
                {
                    // Call was accepted, rejected, or completed before deadline, clear deadline
                    call.TimeoutDeadline = null;
                    call.TimeoutType = null;
                    try
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Concurrency conflict clearing deadline; safely ignore
                    }
                }
            }
            else if (call.TimeoutType == CallTimeoutType.Reconnect)
            {
                // Idempotency Guard: transition only if call is still in Accepted or Ringing status
                if (call.Status == CallStatus.Accepted || call.Status == CallStatus.Ringing)
                {
                    call.Status = CallStatus.Failed;
                    call.MissedReason = MissedReason.ConnectionFailed;
                    call.EndedAt = now;
                    call.UpdatedAt = now;
                    call.TimeoutDeadline = null;
                    call.TimeoutType = null;

                    await _presenceTracker.SetUserPresenceAsync(call.CallerId, PresenceStatus.Online);
                    await _presenceTracker.SetUserPresenceAsync(call.CalleeId, PresenceStatus.Online);

                    var callerUser = await _unitOfWork.Users.GetByIdAsync(call.CallerId, cancellationToken);
                    if (callerUser != null && !callerUser.IsDeleted)
                    {
                        callerUser.PresenceStatus = PresenceStatus.Online;
                        callerUser.UpdatedAt = now;
                    }

                    var calleeUser = await _unitOfWork.Users.GetByIdAsync(call.CalleeId, cancellationToken);
                    if (calleeUser != null && !calleeUser.IsDeleted)
                    {
                        calleeUser.PresenceStatus = PresenceStatus.Online;
                        calleeUser.UpdatedAt = now;
                    }

                    try
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        _logger.LogWarning(ex, "Concurrency conflict processing Reconnect timeout for Call {CallId}. Reloading entity.", call.Id);
                        var entry = ex.Entries.FirstOrDefault();
                        if (entry != null)
                        {
                            await entry.ReloadAsync(cancellationToken);
                        }

                        if (call.Status != CallStatus.Accepted && call.Status != CallStatus.Ringing)
                        {
                            _logger.LogInformation("Call {CallId} status changed to {Status} concurrently; Reconnect timeout is moot.", call.Id, call.Status);
                            continue;
                        }
                    }

                    var callerConn = await _presenceTracker.GetConnectionIdsForUserAsync(call.CallerId);
                    var calleeConn = await _presenceTracker.GetConnectionIdsForUserAsync(call.CalleeId);
                    var allConns = callerConn.Concat(calleeConn).ToList();

                    await _hubContext.Clients.Clients(allConns).CallFailed(
                        call.Id, "Network drop timeout - failed to reconnect within 10 seconds");
                    await _hubContext.Clients.Clients(allConns).CallEnded(call.Id);

                    _logger.LogInformation("Processed Reconnect timeout for Call {CallId}. Marked as Failed (ConnectionFailed).", call.Id);
                }
                else
                {
                    // Call is no longer active, clear deadline
                    call.TimeoutDeadline = null;
                    call.TimeoutType = null;
                    try
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Concurrency conflict clearing deadline; safely ignore
                    }
                }
            }
        }
    }
}
