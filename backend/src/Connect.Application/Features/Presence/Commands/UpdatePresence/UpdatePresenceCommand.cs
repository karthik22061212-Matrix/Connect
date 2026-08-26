using Connect.Application.Features.Presence.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Presence.Commands.UpdatePresence;

public record UpdatePresenceCommand(PresenceStatus Status) : IRequest<PresenceDto>;
