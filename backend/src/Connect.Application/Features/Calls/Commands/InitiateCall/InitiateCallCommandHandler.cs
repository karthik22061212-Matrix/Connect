using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.Calls.Commands.InitiateCall;

public class InitiateCallCommandHandler : IRequestHandler<InitiateCallCommand, CallResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPushNotificationService _pushNotificationService;

    public InitiateCallCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPresenceTracker presenceTracker,
        IDateTimeProvider dateTimeProvider,
        IPushNotificationService pushNotificationService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _presenceTracker = presenceTracker;
        _dateTimeProvider = dateTimeProvider;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<CallResultDto> Handle(InitiateCallCommand request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        if (callerId == request.CalleeId)
        {
            throw new ConflictException("Cannot call yourself.");
        }

        var callerUser = await _unitOfWork.Users.GetByIdAsync(callerId, cancellationToken);
        if (callerUser == null || callerUser.IsDeleted)
        {
            throw new NotFoundException("Caller not found.");
        }

        var calleeUser = await _unitOfWork.Users.GetByIdAsync(request.CalleeId, cancellationToken);
        if (calleeUser == null || calleeUser.IsDeleted)
        {
            throw new NotFoundException("Callee not found.");
        }

        // Check blocks
        var isBlocked = await _unitOfWork.Blocks.AnyAsync(b =>
            (b.BlockerUserId == callerId && b.BlockedUserId == request.CalleeId) ||
            (b.BlockerUserId == request.CalleeId && b.BlockedUserId == callerId), cancellationToken);

        if (isBlocked)
        {
            throw new ForbiddenAccessException("Cannot call this user.");
        }

        // Enforce Connection ordering rule: UserAId < UserBId
        var userAId = callerId.CompareTo(request.CalleeId) < 0 ? callerId : request.CalleeId;
        var userBId = callerId.CompareTo(request.CalleeId) < 0 ? request.CalleeId : callerId;

        var connection = await _unitOfWork.Connections.FirstOrDefaultAsync(
            c => c.UserAId == userAId && c.UserBId == userBId, cancellationToken);

        if (connection == null)
        {
            throw new ForbiddenAccessException("You are not connected with this user.");
        }

        var calleePresence = await _presenceTracker.GetUserPresenceAsync(request.CalleeId);

        if (calleePresence == PresenceStatus.Offline)
        {
            var call = new Call
            {
                Id = Guid.NewGuid(),
                ConnectionId = connection.Id,
                CallerId = callerId,
                CalleeId = request.CalleeId,
                Status = CallStatus.Missed,
                MissedReason = MissedReason.Offline,
                StartedAt = _dateTimeProvider.UtcNow,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            };

            _unitOfWork.Calls.Add(call);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new CallResultDto(call.Id, callerId, request.CalleeId, CallStatus.Missed, MissedReason.Offline, callerUser.UserId);
            }

            await _pushNotificationService.SendMissedCallNotificationAsync(
                request.CalleeId, call.Id, callerUser.UserId, MissedReason.Offline, cancellationToken);

            return new CallResultDto(call.Id, callerId, request.CalleeId, CallStatus.Missed, MissedReason.Offline, callerUser.UserId);
        }

        if (calleePresence == PresenceStatus.Busy)
        {
            var call = new Call
            {
                Id = Guid.NewGuid(),
                ConnectionId = connection.Id,
                CallerId = callerId,
                CalleeId = request.CalleeId,
                Status = CallStatus.Missed,
                MissedReason = MissedReason.Busy,
                StartedAt = _dateTimeProvider.UtcNow,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            };

            _unitOfWork.Calls.Add(call);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new CallResultDto(call.Id, callerId, request.CalleeId, CallStatus.Missed, MissedReason.Busy, callerUser.UserId);
            }

            await _pushNotificationService.SendMissedCallNotificationAsync(
                request.CalleeId, call.Id, callerUser.UserId, MissedReason.Busy, cancellationToken);

            return new CallResultDto(call.Id, callerId, request.CalleeId, CallStatus.Missed, MissedReason.Busy, callerUser.UserId);
        }

        // Callee is Online / Available
        var activeCall = new Call
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            CallerId = callerId,
            CalleeId = request.CalleeId,
            Status = CallStatus.Ringing,
            TimeoutDeadline = _dateTimeProvider.UtcNow.AddSeconds(15),
            TimeoutType = CallTimeoutType.Ring,
            StartedAt = _dateTimeProvider.UtcNow,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        _unitOfWork.Calls.Add(activeCall);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new CallResultDto(activeCall.Id, callerId, request.CalleeId, CallStatus.Ringing, null, callerUser.UserId);
        }

        await _pushNotificationService.SendIncomingCallNotificationAsync(
            request.CalleeId, activeCall.Id, callerUser.UserId, cancellationToken);

        return new CallResultDto(activeCall.Id, callerId, request.CalleeId, CallStatus.Ringing, null, callerUser.UserId);
    }
}
