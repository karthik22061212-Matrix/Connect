using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Blocking.Models;
using MediatR;

namespace Connect.Application.Features.Blocking.Queries.GetBlockedUsers;

public class GetBlockedUsersQueryHandler : IRequestHandler<GetBlockedUsersQuery, IReadOnlyList<BlockedUserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetBlockedUsersQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<BlockedUserDto>> Handle(GetBlockedUsersQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var allBlocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);
        var userBlocks = allBlocks.Where(b => b.BlockerUserId == currentUserId.Value).ToList();

        if (userBlocks.Count == 0)
        {
            return Array.Empty<BlockedUserDto>();
        }

        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);
        var userDict = allUsers.ToDictionary(u => u.Id, u => u.UserId);

        return userBlocks.Select(b => new BlockedUserDto(
            b.BlockedUserId,
            userDict.GetValueOrDefault(b.BlockedUserId, string.Empty),
            b.CreatedAt
        )).ToList();
    }
}
