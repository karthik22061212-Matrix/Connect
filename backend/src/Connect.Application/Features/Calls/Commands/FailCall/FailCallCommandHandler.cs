using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.FailCall;

public class FailCallCommandHandler : IRequestHandler<FailCallCommand, FailCallResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FailCallCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPresenceTracker presenceTracker,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _presenceTracker = presenceTracker;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FailCallResultDto> Handle(FailCallCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;

        var call = await _unitOfWork.Calls.GetByIdAsync(request.CallId, cancellationToken);
        if (call == null)
        {
            throw new NotFoundException("Call not found.");
        }

        if (currentUserId.HasValue && call.CallerId != currentUserId.Value && call.CalleeId != currentUserId.Value)
        {
            throw new ForbiddenAccessException("You are not a participant in this call.");
        }

        var now = _dateTimeProvider.UtcNow;
        call.Status = CallStatus.Failed;
        call.MissedReason = request.Reason;
        call.EndedAt = now;
        call.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reset presence to Online for both caller and callee
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var otherUserId = currentUserId.HasValue
            ? (call.CallerId == currentUserId.Value ? call.CalleeId : call.CallerId)
            : call.CalleeId;

        return new FailCallResultDto(
            call.Id,
            call.CallerId,
            call.CalleeId,
            otherUserId,
            call.Status,
            request.Reason
        );
    }
}
