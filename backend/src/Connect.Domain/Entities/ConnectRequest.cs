using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class ConnectRequest : AuditableEntity
{
    public Guid FromUserId { get; set; }
    public User FromUser { get; set; } = null!;

    public Guid ToUserId { get; set; }
    public User ToUser { get; set; } = null!;

    public ConnectRequestStatus Status { get; set; } = ConnectRequestStatus.Pending;
    public DateTime? RespondedAt { get; set; }
}
