using System.Collections.Concurrent;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;

namespace Connect.Infrastructure.Realtime;

public class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _onlineUsers = new();
    private readonly ConcurrentDictionary<Guid, PresenceStatus> _userStatuses = new();
    private readonly object _lock = new();

    public Task<bool> UserConnectedAsync(Guid userId, string connectionId)
    {
        bool isFirstConnection = false;

        lock (_lock)
        {
            if (!_onlineUsers.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                _onlineUsers[userId] = connections;
                isFirstConnection = true;
            }

            connections.Add(connectionId);

            if (isFirstConnection)
            {
                _userStatuses[userId] = PresenceStatus.Online;
            }
        }

        return Task.FromResult(isFirstConnection);
    }

    public Task<bool> UserDisconnectedAsync(Guid userId, string connectionId)
    {
        bool isLastConnection = false;

        lock (_lock)
        {
            if (_onlineUsers.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                {
                    _onlineUsers.TryRemove(userId, out _);
                    _userStatuses[userId] = PresenceStatus.Offline;
                    isLastConnection = true;
                }
            }
        }

        return Task.FromResult(isLastConnection);
    }

    public Task<IReadOnlyList<Guid>> GetOnlineUsersAsync()
    {
        lock (_lock)
        {
            var onlineUserIds = _onlineUsers.Keys.ToList();
            return Task.FromResult<IReadOnlyList<Guid>>(onlineUserIds);
        }
    }

    public Task<IReadOnlyList<string>> GetConnectionIdsForUserAsync(Guid userId)
    {
        lock (_lock)
        {
            if (_onlineUsers.TryGetValue(userId, out var connections))
            {
                return Task.FromResult<IReadOnlyList<string>>(connections.ToList());
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    public Task<bool> IsUserOnlineAsync(Guid userId)
    {
        lock (_lock)
        {
            return Task.FromResult(_onlineUsers.ContainsKey(userId) && _onlineUsers[userId].Count > 0);
        }
    }

    public Task<PresenceStatus> GetUserPresenceAsync(Guid userId)
    {
        lock (_lock)
        {
            if (!_onlineUsers.ContainsKey(userId) || _onlineUsers[userId].Count == 0)
            {
                return Task.FromResult(PresenceStatus.Offline);
            }

            if (_userStatuses.TryGetValue(userId, out var status))
            {
                return Task.FromResult(status);
            }

            return Task.FromResult(PresenceStatus.Online);
        }
    }

    public Task SetUserPresenceAsync(Guid userId, PresenceStatus status)
    {
        lock (_lock)
        {
            _userStatuses[userId] = status;
        }

        return Task.CompletedTask;
    }
}
