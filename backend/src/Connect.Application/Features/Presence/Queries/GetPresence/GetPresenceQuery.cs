using Connect.Application.Features.Presence.Models;
using MediatR;

namespace Connect.Application.Features.Presence.Queries.GetPresence;

public record GetPresenceQuery(Guid TargetUserId) : IRequest<PresenceDto>;
