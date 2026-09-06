using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class UserPresenceSetting : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public PresenceVisibility PresenceVisibility { get; set; }
}
