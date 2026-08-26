using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Notifications;

public class FcmPushNotificationService : IPushNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FcmPushNotificationService> _logger;

    public FcmPushNotificationService(
        IUnitOfWork unitOfWork,
        ILogger<FcmPushNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SendIncomingCallNotificationAsync(Guid calleeUserId, Guid callId, string callerUserHandle, CancellationToken ct = default)
    {
        var allTokens = await _unitOfWork.DeviceTokens.ListAsync(ct);
        var userTokens = allTokens.Where(t => t.UserId == calleeUserId).ToList();

        if (userTokens.Count == 0)
        {
            _logger.LogInformation("No device tokens registered for callee {CalleeUserId}. Skipping push notification.", calleeUserId);
            return;
        }

        foreach (var token in userTokens)
        {
            _logger.LogInformation(
                "[FCM Push] Sending Incoming Call push notification to user {CalleeUserId} (Platform: {Platform}, Token: {Token}): Call {CallId} from @{CallerHandle}",
                calleeUserId, token.Platform, token.Token, callId, callerUserHandle);
        }
    }

    public async Task SendMissedCallNotificationAsync(Guid calleeUserId, Guid callId, string callerUserHandle, MissedReason reason, CancellationToken ct = default)
    {
        var allTokens = await _unitOfWork.DeviceTokens.ListAsync(ct);
        var userTokens = allTokens.Where(t => t.UserId == calleeUserId).ToList();

        if (userTokens.Count == 0)
        {
            _logger.LogInformation("No device tokens registered for callee {CalleeUserId}. Skipping missed call push notification.", calleeUserId);
            return;
        }

        foreach (var token in userTokens)
        {
            _logger.LogInformation(
                "[FCM Push] Sending Missed Call push notification to user {CalleeUserId} (Platform: {Platform}, Token: {Token}): Call {CallId} from @{CallerHandle}, Reason: {Reason}",
                calleeUserId, token.Platform, token.Token, callId, callerUserHandle, reason);
        }
    }
}
