using Connect.Domain.Enums;
using Connect.Infrastructure.Realtime;

namespace Connect.Application.UnitTests.Presence;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _presenceTracker = new();

    [Fact]
    public async Task UserConnected_FirstConnection_ReturnsTrueAndSetsOnline()
    {
        var userId = Guid.NewGuid();
        var conn1 = "conn-1";

        var isFirst = await _presenceTracker.UserConnectedAsync(userId, conn1);

        Assert.True(isFirst);
        Assert.True(await _presenceTracker.IsUserOnlineAsync(userId));
        Assert.Equal(PresenceStatus.Online, await _presenceTracker.GetUserPresenceAsync(userId));
    }

    [Fact]
    public async Task UserConnected_MultipleConnections_ReturnsFalseOnSecond()
    {
        var userId = Guid.NewGuid();

        var isFirst = await _presenceTracker.UserConnectedAsync(userId, "conn-1");
        var isSecond = await _presenceTracker.UserConnectedAsync(userId, "conn-2");

        Assert.True(isFirst);
        Assert.False(isSecond);

        var connections = await _presenceTracker.GetConnectionIdsForUserAsync(userId);
        Assert.Equal(2, connections.Count);
    }

    [Fact]
    public async Task UserDisconnected_LastConnection_ReturnsTrueAndSetsOffline()
    {
        var userId = Guid.NewGuid();
        await _presenceTracker.UserConnectedAsync(userId, "conn-1");
        await _presenceTracker.UserConnectedAsync(userId, "conn-2");

        var isLast1 = await _presenceTracker.UserDisconnectedAsync(userId, "conn-1");
        Assert.False(isLast1);
        Assert.True(await _presenceTracker.IsUserOnlineAsync(userId));

        var isLast2 = await _presenceTracker.UserDisconnectedAsync(userId, "conn-2");
        Assert.True(isLast2);
        Assert.False(await _presenceTracker.IsUserOnlineAsync(userId));
        Assert.Equal(PresenceStatus.Offline, await _presenceTracker.GetUserPresenceAsync(userId));
    }

    [Fact]
    public async Task SetUserPresence_UpdatesPresenceStatus()
    {
        var userId = Guid.NewGuid();
        await _presenceTracker.UserConnectedAsync(userId, "conn-1");

        await _presenceTracker.SetUserPresenceAsync(userId, PresenceStatus.Busy);

        Assert.Equal(PresenceStatus.Busy, await _presenceTracker.GetUserPresenceAsync(userId));
    }
}
