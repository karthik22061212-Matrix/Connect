using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Connections.Models;
using MediatR;

namespace Connect.Application.Features.Connections.Queries.GetConnections;

public class GetConnectionsQueryHandler : IRequestHandler<GetConnectionsQuery, IEnumerable<ConnectionDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetConnectionsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<ConnectionDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var allConnections = await _unitOfWork.Connections.ListAsync(cancellationToken);
        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);

        var userConnections = allConnections
            .Where(c => c.UserAId == currentUserId || c.UserBId == currentUserId)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var dtos = new List<ConnectionDto>();

        foreach (var conn in userConnections)
        {
            var contactId = conn.UserAId == currentUserId ? conn.UserBId : conn.UserAId;
            var contact = allUsers.FirstOrDefault(u => u.Id == contactId);

            if (contact != null && !contact.IsDeleted)
            {
                dtos.Add(new ConnectionDto(
                    conn.Id,
                    contact.Id,
                    contact.UserId,
                    contact.Email,
                    contact.PhoneNumber,
                    contact.PresenceStatus,
                    conn.CreatedAt
                ));
            }
        }

        return dtos;
    }
}
