using Connect.Domain.Enums;

namespace Connect.Application.Features.Calls.Models;

public record FailCallResultDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    Guid OtherUserId,
    CallStatus Status,
    MissedReason MissedReason
);
