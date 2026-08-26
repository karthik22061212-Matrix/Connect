using Connect.Domain.Enums;

namespace Connect.Application.Features.ConnectRequests.Models;

public record ConnectRequestDto(
    Guid Id,
    Guid FromUserId,
    Guid ToUserId,
    ConnectRequestStatus Status,
    DateTime CreatedAt,
    DateTime? RespondedAt
);
