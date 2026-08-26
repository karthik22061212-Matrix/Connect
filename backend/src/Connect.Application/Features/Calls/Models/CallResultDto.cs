using Connect.Domain.Enums;

namespace Connect.Application.Features.Calls.Models;

public record CallResultDto(
    Guid? CallId,
    Guid CallerId,
    Guid CalleeId,
    CallStatus Status,
    MissedReason? MissedReason = null,
    string? CallerUserIdHandle = null
);
