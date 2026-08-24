using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class User : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public PresenceStatus PresenceStatus { get; set; } = PresenceStatus.Offline;
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime? ReactivationDeadline { get; set; }

    // Navigation properties
    public ICollection<ConnectRequest> ConnectRequestsSent { get; set; } = new List<ConnectRequest>();
    public ICollection<ConnectRequest> ConnectRequestsReceived { get; set; } = new List<ConnectRequest>();
    public ICollection<Connection> ConnectionsA { get; set; } = new List<Connection>();
    public ICollection<Connection> ConnectionsB { get; set; } = new List<Connection>();
    public ICollection<Call> CallsMade { get; set; } = new List<Call>();
    public ICollection<Call> CallsReceived { get; set; } = new List<Call>();
    public ICollection<Block> BlocksInitiated { get; set; } = new List<Block>();
    public ICollection<Block> BlocksReceived { get; set; } = new List<Block>();
    public ICollection<Report> ReportsMade { get; set; } = new List<Report>();
    public ICollection<Report> ReportsReceived { get; set; } = new List<Report>();
    public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();
}
