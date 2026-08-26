using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.EndCall;

public class EndCallCommandHandler : IRequestHandler<EndCallCommand, EndCallResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IDateTimeProvider _dateTimeProvider;

    public EndCallCommandHandler(
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

    public async Task<EndCallResultDto> Handle(EndCallCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var call = await _unitOfWork.Calls.GetByIdAsync(request.CallId, cancellationToken);
        if (call == null)
        {
            throw new NotFoundException("Call not found.");
        }

        if (call.CallerId != userId && call.CalleeId != userId)
        {
            throw new ForbiddenAccessException("You are not a participant in this call.");
        }

        var now = _dateTimeProvider.UtcNow;
        call.Status = CallStatus.Completed;
        call.EndedAt = now;
        if (call.AnsweredAt.HasValue)
        {
            call.DurationSeconds = (int)(now - call.AnsweredAt.Value).TotalSeconds;
        }
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

        var otherUserId = call.CallerId == userId ? call.CalleeId : call.CallerId;

        return new EndCallResultDto(
            call.Id,
            call.CallerId,
            call.CalleeId,
            otherUserId,
            call.DurationSeconds
        );
    }
}
