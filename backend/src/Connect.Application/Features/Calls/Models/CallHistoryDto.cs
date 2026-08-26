using Connect.Domain.Enums;

namespace Connect.Application.Features.Calls.Models;

public record CallHistoryDto(
    Guid Id,
    Guid CallerId,
    string CallerUserId,
    Guid CalleeId,
    string CalleeUserId,
    bool IsOutgoing,
    CallStatus Status,
    MissedReason? MissedReason,
    DateTime StartedAt,
    DateTime? AnsweredAt,
    DateTime? EndedAt,
    int? DurationSeconds
);
