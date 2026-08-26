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

    public GetPresenceQueryHandler(
        IUnitOfWork unitOfWork,
        IPresenceTracker presenceTracker,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _presenceTracker = presenceTracker;
        _currentUserService = currentUserService;
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
            var minId = currentUserId.CompareTo(request.TargetUserId) < 0 ? currentUserId : request.TargetUserId;
            var maxId = currentUserId.CompareTo(request.TargetUserId) < 0 ? request.TargetUserId : currentUserId;

            var connections = await _unitOfWork.Connections.ListAsync(cancellationToken);
            var isConnected = connections.Any(c => c.UserAId == minId && c.UserBId == maxId);

            if (!isConnected)
            {
                throw new ForbiddenAccessException("You can only view presence status for connected users.");
            }
        }

        var presence = await _presenceTracker.GetUserPresenceAsync(request.TargetUserId);
        return new PresenceDto(user.Id, presence, user.UpdatedAt);
    }
}
