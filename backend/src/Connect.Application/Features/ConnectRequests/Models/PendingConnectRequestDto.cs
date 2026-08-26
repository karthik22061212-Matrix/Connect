namespace Connect.Application.Features.ConnectRequests.Models;

public record PendingConnectRequestDto(
    Guid Id,
    Guid FromUserId,
    string FromUserHandle,
    string FromUserEmail,
    string? FromUserPhoneNumber,
    DateTime CreatedAt
);
