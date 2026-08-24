using Connect.Domain.Common;

namespace Connect.Domain.Entities;

public class Connection : AuditableEntity
{
    public Guid UserAId { get; set; }
    public User UserA { get; set; } = null!;

    public Guid UserBId { get; set; }
    public User UserB { get; set; } = null!;

    public ICollection<Call> Calls { get; set; } = new List<Call>();
}
