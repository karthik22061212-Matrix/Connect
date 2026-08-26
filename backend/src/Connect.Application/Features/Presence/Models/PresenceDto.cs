using Connect.Domain.Enums;

namespace Connect.Application.Features.Presence.Models;

public record PresenceDto(
    Guid UserId,
    PresenceStatus Status,
    DateTime UpdatedAt
);
