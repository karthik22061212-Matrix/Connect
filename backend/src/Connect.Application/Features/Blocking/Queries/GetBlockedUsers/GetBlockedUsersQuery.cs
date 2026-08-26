using Connect.Application.Features.Blocking.Models;
using MediatR;

namespace Connect.Application.Features.Blocking.Queries.GetBlockedUsers;

public record GetBlockedUsersQuery : IRequest<IReadOnlyList<BlockedUserDto>>;
