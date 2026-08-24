using Connect.Domain.Common;
using Connect.Domain.Enums;

namespace Connect.Domain.Entities;

public class Report : AuditableEntity
{
    public Guid ReporterUserId { get; set; }
    public User ReporterUser { get; set; } = null!;

    public Guid ReportedUserId { get; set; }
    public User ReportedUser { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
}
