using Connect.Domain.Common;

namespace Connect.Domain.Entities;

public class Block : AuditableEntity
{
    public Guid BlockerUserId { get; set; }
    public User BlockerUser { get; set; } = null!;

    public Guid BlockedUserId { get; set; }
    public User BlockedUser { get; set; } = null!;
}
