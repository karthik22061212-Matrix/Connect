using Connect.Domain.Enums;

namespace Connect.Infrastructure.Realtime;

public interface ICallHubClient
{
    Task UserPresenceChanged(Guid userId, PresenceStatus status);
    Task IncomingCall(Guid callId, Guid callerId, string callerUserId);
    Task CalleeUnavailable(Guid calleeId, string reason);
    Task CalleeBusy(Guid calleeId);
    Task MissedCallNotification(Guid callId, Guid callerId, string callerUserId, DateTime timestamp);
    Task CallAccepted(Guid callId);
    Task CallRejected(Guid callId);
    Task CallEnded(Guid callId);
    Task CallTimeout(Guid callId);
    Task ReceiveWebRtcOffer(Guid callId, string sdp);
    Task ReceiveWebRtcAnswer(Guid callId, string sdp);
    Task ReceiveIceCandidate(Guid callId, string candidate);
    Task NetworkReconnecting(Guid callId);
    Task NetworkRestored(Guid callId);
    Task CallFailed(Guid callId, string reason);
    Task ConnectRequestReceived(Guid requestId, Guid fromUserId, string fromUserHandle);
    Task ConnectRequestAccepted(Guid requestId, Guid contactId, string contactUserId);
    Task ConnectRequestDeclined(Guid requestId, Guid decliningUserId);
}

