using Connect.Domain.Enums;

namespace Connect.Application.Common.Interfaces;

public interface IPresenceTracker
{
    Task<bool> UserConnectedAsync(Guid userId, string connectionId);
    Task<bool> UserDisconnectedAsync(Guid userId, string connectionId);
    Task<IReadOnlyList<Guid>> GetOnlineUsersAsync();
    Task<IReadOnlyList<string>> GetConnectionIdsForUserAsync(Guid userId);
    Task<bool> IsUserOnlineAsync(Guid userId);
    Task<PresenceStatus> GetUserPresenceAsync(Guid userId);
    Task SetUserPresenceAsync(Guid userId, PresenceStatus status);
}
