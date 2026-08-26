using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Users.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Users.Queries.SearchUsers;

public class SearchUsersQueryHandler : IRequestHandler<SearchUsersQuery, IEnumerable<UserSearchResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SearchUsersQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<UserSearchResultDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var queryStr = request.Query.Trim();

        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);
        var allConnections = await _unitOfWork.Connections.ListAsync(cancellationToken);
        var allRequests = await _unitOfWork.ConnectRequests.ListAsync(cancellationToken);
        var allBlocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);

        // Filter out soft-deleted users and current user
        var candidates = allUsers
            .Where(u => !u.IsDeleted && (currentUserId == null || u.Id != currentUserId.Value))
            .ToList();

        // Filter out blocked users (in either direction)
        if (currentUserId != null)
        {
            var blockedUserIds = allBlocks
                .Where(b => b.BlockerUserId == currentUserId.Value || b.BlockedUserId == currentUserId.Value)
                .Select(b => b.BlockerUserId == currentUserId.Value ? b.BlockedUserId : b.BlockerUserId)
                .ToHashSet();

            candidates = candidates.Where(u => !blockedUserIds.Contains(u.Id)).ToList();
        }

        // Match query against UserId or PhoneNumber
        var matchedUsers = candidates.Where(u =>
            u.UserId.Contains(queryStr, StringComparison.OrdinalIgnoreCase) ||
            (u.PhoneNumber != null && u.PhoneNumber.Contains(queryStr, StringComparison.OrdinalIgnoreCase)) ||
            u.Email.Contains(queryStr, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        var results = new List<UserSearchResultDto>();

        foreach (var user in matchedUsers)
        {
            bool isConnected = false;
            bool hasPendingRequest = false;
            Guid? pendingRequestId = null;

            if (currentUserId != null)
            {
                var minId = currentUserId.Value.CompareTo(user.Id) < 0 ? currentUserId.Value : user.Id;
                var maxId = currentUserId.Value.CompareTo(user.Id) < 0 ? user.Id : currentUserId.Value;

                isConnected = allConnections.Any(c => c.UserAId == minId && c.UserBId == maxId);

                var pendingReq = allRequests.FirstOrDefault(r =>
                    r.Status == ConnectRequestStatus.Pending &&
                    ((r.FromUserId == currentUserId.Value && r.ToUserId == user.Id) ||
                     (r.FromUserId == user.Id && r.ToUserId == currentUserId.Value)));

                if (pendingReq != null)
                {
                    hasPendingRequest = true;
                    pendingRequestId = pendingReq.Id;
                }
            }

            results.Add(new UserSearchResultDto(
                user.Id,
                user.UserId,
                user.Email,
                user.PhoneNumber,
                user.PresenceStatus,
                isConnected,
                hasPendingRequest,
                pendingRequestId
            ));
        }

        return results;
    }
}
