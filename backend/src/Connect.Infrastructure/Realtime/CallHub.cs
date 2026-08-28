using System.Security.Claims;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.EndCall;
using Connect.Application.Features.Calls.Commands.FailCall;
using Connect.Application.Features.Calls.Commands.InitiateCall;
using Connect.Application.Features.Calls.Commands.RecordNetworkDrop;
using Connect.Application.Features.Calls.Commands.RecordNetworkRestored;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Realtime;

[Authorize]
public class CallHub : Hub<ICallHubClient>
{
    private readonly IPresenceTracker _presenceTracker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISender _mediator;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CallHub> _logger;

    public CallHub(
        IPresenceTracker presenceTracker,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ISender mediator,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CallHub> logger)
    {
        _presenceTracker = presenceTracker;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _mediator = mediator;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var isFirstConnection = await _presenceTracker.UserConnectedAsync(userId, Context.ConnectionId);

        if (isFirstConnection)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, CancellationToken.None);
            if (user != null && !user.IsDeleted)
            {
                user.PresenceStatus = PresenceStatus.Online;
                user.UpdatedAt = _dateTimeProvider.UtcNow;
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }

            await Clients.Others.UserPresenceChanged(userId, PresenceStatus.Online);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var isLastConnection = await _presenceTracker.UserDisconnectedAsync(userId, Context.ConnectionId);

        if (isLastConnection)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, CancellationToken.None);
            if (user != null && !user.IsDeleted)
            {
                user.PresenceStatus = PresenceStatus.Offline;
                user.UpdatedAt = _dateTimeProvider.UtcNow;
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }

            await Clients.Others.UserPresenceChanged(userId, PresenceStatus.Offline);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task UpdatePresence(PresenceStatus status)
    {
        var userId = GetUserId();
        await _presenceTracker.SetUserPresenceAsync(userId, status);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, CancellationToken.None);
        if (user != null && !user.IsDeleted)
        {
            user.PresenceStatus = status;
            user.UpdatedAt = _dateTimeProvider.UtcNow;
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await Clients.Others.UserPresenceChanged(userId, status);
    }

    public async Task InitiateCallAttempt(Guid calleeId)
    {
        try
        {
            var result = await _mediator.Send(new InitiateCallCommand(calleeId));

            if (result.Status == CallStatus.Missed)
            {
                if (result.MissedReason == MissedReason.Offline)
                {
                    await Clients.Caller.CalleeUnavailable(calleeId, "Offline");
                }
                else if (result.MissedReason == MissedReason.Busy)
                {
                    await Clients.Caller.CalleeBusy(calleeId);

                    var calleeConnections = await _presenceTracker.GetConnectionIdsForUserAsync(calleeId);
                    if (calleeConnections.Count > 0 && result.CallId.HasValue)
                    {
                        await Clients.Clients(calleeConnections).MissedCallNotification(
                            result.CallId.Value,
                            result.CallerId,
                            result.CallerUserIdHandle ?? "",
                            _dateTimeProvider.UtcNow
                        );
                    }
                }
                return;
            }

            if (result.Status == CallStatus.Ringing && result.CallId.HasValue)
            {
                var callId = result.CallId.Value;
                var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(calleeId);
                await Clients.Clients(targetConnections).IncomingCall(callId, result.CallerId, result.CallerUserIdHandle ?? "");
            }
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task RespondToCall(Guid callId, bool accepted)
    {
        var userId = GetUserId();
        var call = await _unitOfWork.Calls.GetByIdAsync(callId, CancellationToken.None);
        if (call == null || call.CalleeId != userId)
        {
            throw new HubException("Call not found or unauthorized.");
        }

        if (accepted)
        {
            call.Status = CallStatus.Accepted;
            call.AnsweredAt = _dateTimeProvider.UtcNow;
            call.UpdatedAt = _dateTimeProvider.UtcNow;
            call.TimeoutDeadline = null;
            call.TimeoutType = null;
            try
            {
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.FirstOrDefault();
                if (entry != null)
                {
                    await entry.ReloadAsync(CancellationToken.None);
                }
                if (call.Status != CallStatus.Accepted)
                {
                    return;
                }
            }

            // Set both users to Busy during active call
            await UpdatePresence(PresenceStatus.Busy);
            await _presenceTracker.SetUserPresenceAsync(call.CallerId, PresenceStatus.Busy);

            var callerConnections = await _presenceTracker.GetConnectionIdsForUserAsync(call.CallerId);
            await Clients.Clients(callerConnections).CallAccepted(callId);
        }
        else
        {
            call.Status = CallStatus.Rejected;
            call.EndedAt = _dateTimeProvider.UtcNow;
            call.UpdatedAt = _dateTimeProvider.UtcNow;
            call.TimeoutDeadline = null;
            call.TimeoutType = null;
            try
            {
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.FirstOrDefault();
                if (entry != null)
                {
                    await entry.ReloadAsync(CancellationToken.None);
                }
                if (call.Status != CallStatus.Rejected)
                {
                    return;
                }
            }

            var callerConnections = await _presenceTracker.GetConnectionIdsForUserAsync(call.CallerId);
            await Clients.Clients(callerConnections).CallRejected(callId);
        }
    }

    public async Task EndCall(Guid callId)
    {
        try
        {
            var result = await _mediator.Send(new EndCallCommand(callId));

            var otherConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.OtherUserId);
            await Clients.Clients(otherConnections).CallEnded(callId);
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task SendWebRtcOffer(Guid callId, string sdp)
    {
        var userId = GetUserId();
        var call = await _unitOfWork.Calls.GetByIdAsync(callId, CancellationToken.None);
        if (call == null) return;

        EnsureCallParticipant(call, userId);

        var targetUserId = call.CallerId == userId ? call.CalleeId : call.CallerId;
        var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(targetUserId);
        await Clients.Clients(targetConnections).ReceiveWebRtcOffer(callId, sdp);
    }

    public async Task SendWebRtcAnswer(Guid callId, string sdp)
    {
        var userId = GetUserId();
        var call = await _unitOfWork.Calls.GetByIdAsync(callId, CancellationToken.None);
        if (call == null) return;

        EnsureCallParticipant(call, userId);

        var targetUserId = call.CallerId == userId ? call.CalleeId : call.CallerId;
        var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(targetUserId);
        await Clients.Clients(targetConnections).ReceiveWebRtcAnswer(callId, sdp);
    }

    public async Task SendIceCandidate(Guid callId, string candidate)
    {
        var userId = GetUserId();
        var call = await _unitOfWork.Calls.GetByIdAsync(callId, CancellationToken.None);
        if (call == null) return;

        EnsureCallParticipant(call, userId);

        var targetUserId = call.CallerId == userId ? call.CalleeId : call.CallerId;
        var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(targetUserId);
        await Clients.Clients(targetConnections).ReceiveIceCandidate(callId, candidate);
    }

    private void EnsureCallParticipant(Call call, Guid userId)
    {
        if (call.CallerId != userId && call.CalleeId != userId)
        {
            _logger.LogWarning("Unauthorized WebRTC operation attempt: User {UserId} is not a participant in Call {CallId}.", userId, call.Id);
            throw new HubException("Unauthorized call access.");
        }
    }

    public async Task NotifyNetworkDrop(Guid callId)
    {
        try
        {
            var result = await _mediator.Send(new RecordNetworkDropCommand(callId));

            var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.OtherUserId);
            await Clients.Clients(targetConnections).NetworkReconnecting(callId);
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task NotifyNetworkRestored(Guid callId)
    {
        try
        {
            var result = await _mediator.Send(new RecordNetworkRestoredCommand(callId));

            var targetConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.OtherUserId);
            await Clients.Clients(targetConnections).NetworkRestored(callId);
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task NotifyCallFailed(Guid callId, string reason)
    {
        try
        {
            var result = await _mediator.Send(new FailCallCommand(callId, MissedReason.ConnectionFailed));

            var callerConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.CallerId);
            var calleeConnections = await _presenceTracker.GetConnectionIdsForUserAsync(result.CalleeId);
            var allConnections = callerConnections.Concat(calleeConnections).ToList();

            await Clients.Clients(allConnections).CallFailed(callId, reason);

            if (calleeConnections.Count > 0)
            {
                var callerUser = await _unitOfWork.Users.GetByIdAsync(result.CallerId, CancellationToken.None);
                await Clients.Clients(calleeConnections).MissedCallNotification(
                    callId,
                    result.CallerId,
                    callerUser?.UserId ?? "",
                    _dateTimeProvider.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }


    private Guid GetUserId()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var userId))
        {
            return userId;
        }

        throw new HubException("Unauthorized: User ID not found.");
    }
}
