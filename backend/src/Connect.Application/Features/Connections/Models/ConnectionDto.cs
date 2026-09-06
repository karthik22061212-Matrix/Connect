using Connect.Domain.Enums;

namespace Connect.Application.Features.Connections.Models;

public record ConnectionDto(
    Guid ConnectionId,
    Guid ContactId,
    string ContactUserId,
    string ContactEmail,
    string? ContactPhoneNumber,
    PresenceStatus? PresenceStatus,
    DateTime ConnectedAt
);
