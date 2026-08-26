namespace Connect.Application.Features.Users.Models;

public record UserIdAvailabilityDto(
    string UserId,
    bool IsAvailable
);
