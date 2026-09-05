using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Presence.Models;
using MediatR;

namespace Connect.Application.Features.Presence.Queries.GetPresence;

public class GetPresenceQueryHandler : IRequestHandler<GetPresenceQuery, PresenceDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPresenceTracker _presenceTracker;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPresenceVisibilityService _presenceVisibilityService;

    public GetPresenceQueryHandler(
        IUnitOfWork unitOfWork,
        IPresenceTracker presenceTracker,
        ICurrentUserService currentUserService,
        IPresenceVisibilityService presenceVisibilityService)
    {
        _unitOfWork = unitOfWork;
        _presenceTracker = presenceTracker;
        _currentUserService = currentUserService;
        _presenceVisibilityService = presenceVisibilityService;
    }

    public async Task<PresenceDto> Handle(GetPresenceQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await _unitOfWork.Users.GetByIdAsync(request.TargetUserId, cancellationToken);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User not found.");
        }

        if (currentUserId != request.TargetUserId)
        {
            var canView = await _presenceVisibilityService.CanViewPresenceAsync(request.TargetUserId, currentUserId, cancellationToken);
            if (!canView)
            {
                throw new ForbiddenAccessException("You are not authorized to view this user's presence.");
            }
        }

        var presence = await _presenceTracker.GetUserPresenceAsync(request.TargetUserId);
        return new PresenceDto(user.Id, presence, user.UpdatedAt);
    }
}
