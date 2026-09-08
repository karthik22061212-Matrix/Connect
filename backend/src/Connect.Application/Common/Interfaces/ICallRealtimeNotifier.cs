namespace Connect.Application.Common.Interfaces;

public interface ICallRealtimeNotifier
{
    Task TerminateActiveCallsBetweenUsersAsync(Guid userAId, Guid userBId, string reason, CancellationToken cancellationToken);
    Task NotifyConnectionRemovedAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken);
}
