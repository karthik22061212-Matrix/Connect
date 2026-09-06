namespace Connect.Application.Common.Diagnostics;

public interface IDiagnosticLogService
{
    void LogEvent(DiagnosticEvent diagnosticEvent);
    void IngestClientLogs(string userId, IEnumerable<DiagnosticEvent> logs);
    IEnumerable<DiagnosticEvent> GetCombinedLogs(string userId);
    void ClearLogs(string userId);
}
