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
        var queryStr = request.Query?.Trim() ?? string.Empty;

        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);
        var allConnections = await _unitOfWork.Connections.ListAsync(cancellationToken);
        var allRequests = await _unitOfWork.ConnectRequests.ListAsync(cancellationToken);
        var allBlocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);
        var allReports = await _unitOfWork.Reports.ListAsync(cancellationToken);

        // Identify reported users in either direction for mutual exclusion
        var reportedUserIds = currentUserId != null
            ? allReports
                .Where(r => r.ReporterUserId == currentUserId.Value || r.ReportedUserId == currentUserId.Value)
                .Select(r => r.ReporterUserId == currentUserId.Value ? r.ReportedUserId : r.ReporterUserId)
                .ToHashSet()
            : new HashSet<Guid>();

        // Filter candidates: soft-deleted, self, and reported users excluded
        var candidates = allUsers
            .Where(u => !u.IsDeleted &&
                        (currentUserId == null || u.Id != currentUserId.Value) &&
                        !reportedUserIds.Contains(u.Id))
            .ToList();

        // Identify users explicitly blocked by current user
        var usersBlockedByMe = currentUserId != null
            ? allBlocks
                .Where(b => b.BlockerUserId == currentUserId.Value)
                .Select(b => b.BlockedUserId)
                .ToHashSet()
            : new HashSet<Guid>();

        // Match query against UserId, PhoneNumber, or Email
        var matchedUsers = candidates.Where(u =>
            u.UserId.Contains(queryStr, StringComparison.OrdinalIgnoreCase) ||
            (u.PhoneNumber != null && u.PhoneNumber.Contains(queryStr, StringComparison.OrdinalIgnoreCase)) ||
            u.Email.Contains(queryStr, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        var results = new List<UserSearchResultDto>();

        foreach (var user in matchedUsers)
        {
            RelationshipState state = RelationshipState.Available;
            Guid? pendingRequestId = null;

            if (currentUserId != null)
            {
                if (usersBlockedByMe.Contains(user.Id))
                {
                    state = RelationshipState.Blocked;
                }
                else
                {
                    var minId = currentUserId.Value.CompareTo(user.Id) < 0 ? currentUserId.Value : user.Id;
                    var maxId = currentUserId.Value.CompareTo(user.Id) < 0 ? user.Id : currentUserId.Value;

                    var isConnected = allConnections.Any(c => c.UserAId == minId && c.UserBId == maxId);

                    if (isConnected)
                    {
                        state = RelationshipState.Connected;
                    }
                    else
                    {
                        var outgoingPendingRequest = allRequests.FirstOrDefault(r =>
                            r.Status == ConnectRequestStatus.Pending &&
                            r.FromUserId == currentUserId.Value && r.ToUserId == user.Id);

                        var incomingPendingRequest = allRequests.FirstOrDefault(r =>
                            r.Status == ConnectRequestStatus.Pending &&
                            r.FromUserId == user.Id && r.ToUserId == currentUserId.Value);

                        if (outgoingPendingRequest != null)
                        {
                            state = RelationshipState.PendingSent;
                            pendingRequestId = outgoingPendingRequest.Id;
                        }
                        else if (incomingPendingRequest != null)
                        {
                            state = RelationshipState.PendingReceived;
                            pendingRequestId = incomingPendingRequest.Id;
                        }
                    }
                }
            }

            results.Add(new UserSearchResultDto(
                user.Id,
                user.UserId,
                user.Email,
                user.PhoneNumber,
                user.PresenceStatus,
                state,
                pendingRequestId
            ));
        }

        return results;
    }
}
