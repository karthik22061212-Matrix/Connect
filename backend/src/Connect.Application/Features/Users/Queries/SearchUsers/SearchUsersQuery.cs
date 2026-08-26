using Connect.Application.Features.Users.Models;
using MediatR;

namespace Connect.Application.Features.Users.Queries.SearchUsers;

public record SearchUsersQuery(
    string Query
) : IRequest<IEnumerable<UserSearchResultDto>>;
