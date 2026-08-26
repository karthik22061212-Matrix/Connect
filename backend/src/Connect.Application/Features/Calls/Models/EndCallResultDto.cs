namespace Connect.Application.Features.Calls.Models;

public record EndCallResultDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    Guid OtherUserId,
    int? DurationSeconds
);
