namespace Connect.Application.Features.Auth.Models;

public record AuthResponseDto(
    Guid Id,
    string UserId,
    string Email,
    string? PhoneNumber,
    string Token
);
