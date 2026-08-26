using Connect.Domain.Enums;

namespace Connect.Application.Common.Interfaces;

public interface IPushNotificationService
{
    Task SendIncomingCallNotificationAsync(Guid calleeUserId, Guid callId, string callerUserHandle, CancellationToken ct = default);
    Task SendMissedCallNotificationAsync(Guid calleeUserId, Guid callId, string callerUserHandle, MissedReason reason, CancellationToken ct = default);
}
