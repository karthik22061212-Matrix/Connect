namespace Connect.Application.Features.Blocking.Models;

public record BlockedUserDto(
    Guid UserId,
    string Handle,
    DateTime BlockedAt
);
