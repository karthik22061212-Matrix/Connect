using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class Call : AuditableEntity
{
    public Guid ConnectionId { get; set; }
    public Connection Connection { get; set; } = null!;

    public Guid CallerId { get; set; }
    public User Caller { get; set; } = null!;

    public Guid CalleeId { get; set; }
    public User Callee { get; set; } = null!;

    public CallStatus Status { get; set; }
    public MissedReason? MissedReason { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? TimeoutDeadline { get; set; }
    public CallTimeoutType? TimeoutType { get; set; }
}
