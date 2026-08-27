using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Queries.GetPendingRequests;

public class GetPendingRequestsQueryHandler : IRequestHandler<GetPendingRequestsQuery, IEnumerable<PendingConnectRequestDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingRequestsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<PendingConnectRequestDto>> Handle(GetPendingRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var allRequests = await _unitOfWork.ConnectRequests.ListAsync(cancellationToken);
        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);
        var allBlocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);

        var blockedUserIds = allBlocks
            .Where(b => b.BlockerUserId == currentUserId || b.BlockedUserId == currentUserId)
            .Select(b => b.BlockerUserId == currentUserId ? b.BlockedUserId : b.BlockerUserId)
            .ToHashSet();

        var pendingRequests = allRequests
            .Where(r => r.ToUserId == currentUserId && r.Status == ConnectRequestStatus.Pending)
            .Where(r => !blockedUserIds.Contains(r.FromUserId))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var dtos = new List<PendingConnectRequestDto>();

        foreach (var req in pendingRequests)
        {
            var sender = allUsers.FirstOrDefault(u => u.Id == req.FromUserId);
            if (sender != null && !sender.IsDeleted)
            {
                dtos.Add(new PendingConnectRequestDto(
                    req.Id,
                    req.FromUserId,
                    sender.UserId,
                    sender.Email,
                    sender.PhoneNumber,
                    req.CreatedAt
                ));
            }
        }

        return dtos;
    }
}
