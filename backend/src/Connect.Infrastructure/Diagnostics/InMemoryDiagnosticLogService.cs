using System.Collections.Concurrent;
using Connect.Application.Common.Diagnostics;

namespace Connect.Infrastructure.Diagnostics;

public class InMemoryDiagnosticLogService : IDiagnosticLogService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DiagnosticEvent>> _userLogs = new();
    private const int MaxEventsPerUser = 1000;

    public void LogEvent(DiagnosticEvent diagnosticEvent)
    {
        if (string.IsNullOrEmpty(diagnosticEvent.UserId)) return;

        var queue = _userLogs.GetOrAdd(diagnosticEvent.UserId, _ => new ConcurrentQueue<DiagnosticEvent>());
        queue.Enqueue(diagnosticEvent);

        while (queue.Count > MaxEventsPerUser)
        {
            queue.TryDequeue(out _);
        }
    }

    public void IngestClientLogs(string userId, IEnumerable<DiagnosticEvent> logs)
    {
        var queue = _userLogs.GetOrAdd(userId, _ => new ConcurrentQueue<DiagnosticEvent>());

        // Enforce ownership
        foreach (var log in logs)
        {
            log.UserId = userId; // Override any client-provided userId with the authenticated one
            queue.Enqueue(log);
        }

        while (queue.Count > MaxEventsPerUser)
        {
            queue.TryDequeue(out _);
        }
    }

    public IEnumerable<DiagnosticEvent> GetCombinedLogs(string userId)
    {
        if (_userLogs.TryGetValue(userId, out var queue))
        {
            // Sort by timestamp
            return queue.OrderBy(e => e.Timestamp).ToList();
        }
        return Enumerable.Empty<DiagnosticEvent>();
    }

    public void ClearLogs(string userId)
    {
        if (_userLogs.TryGetValue(userId, out var queue))
        {
            queue.Clear();
        }
    }
}
