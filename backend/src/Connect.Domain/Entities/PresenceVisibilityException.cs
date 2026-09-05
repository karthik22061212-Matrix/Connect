using Connect.Domain.Common;

namespace Connect.Domain.Entities;

public class PresenceVisibilityException : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;

    public bool IsAllowed { get; set; }
}
