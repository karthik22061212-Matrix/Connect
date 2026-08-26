using Connect.Application.Features.Users.Models;
using MediatR;

namespace Connect.Application.Features.Users.Queries.CheckUserIdAvailability;

public record CheckUserIdAvailabilityQuery(
    string UserId
) : IRequest<UserIdAvailabilityDto>;
