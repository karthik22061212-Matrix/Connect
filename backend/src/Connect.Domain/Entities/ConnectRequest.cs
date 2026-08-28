using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class ConnectRequest : AuditableEntity
{
    public Guid FromUserId { get; set; }
    public User FromUser { get; set; } = null!;

    public Guid ToUserId { get; set; }
    public User ToUser { get; set; } = null!;

    public Guid CanonicalUserAId { get; set; }
    public Guid CanonicalUserBId { get; set; }

    public ConnectRequestStatus Status { get; set; } = ConnectRequestStatus.Pending;
    public DateTime? RespondedAt { get; set; }

    public void SetCanonicalUserIds()
    {
        CanonicalUserAId = FromUserId.CompareTo(ToUserId) < 0 ? FromUserId : ToUserId;
        CanonicalUserBId = FromUserId.CompareTo(ToUserId) < 0 ? ToUserId : FromUserId;
    }
}
