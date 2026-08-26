using Connect.Domain.Enums;

namespace Connect.Application.Features.Users.Models;

public record UserSearchResultDto(
    Guid Id,
    string UserId,
    string Email,
    string? PhoneNumber,
    PresenceStatus PresenceStatus,
    bool IsConnected,
    bool HasPendingRequest,
    Guid? PendingRequestId
);
