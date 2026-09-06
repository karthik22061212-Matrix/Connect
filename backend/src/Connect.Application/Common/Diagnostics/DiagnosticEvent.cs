namespace Connect.Application.Common.Diagnostics;

public class DiagnosticEvent
{
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Component { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CallId { get; set; }
    public object? Metadata { get; set; }
}
