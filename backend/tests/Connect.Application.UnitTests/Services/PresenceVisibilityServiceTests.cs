using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.Services;

public class PresenceVisibilityServiceTests : IDisposable
{
    private readonly ApplicationDbContextMock _context;
    private readonly PresenceVisibilityService _service;

    public PresenceVisibilityServiceTests()
    {
        _context = new ApplicationDbContextMock();
        _service = new PresenceVisibilityService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CanViewPresenceAsync_SameUser_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var result = await _service.CanViewPresenceAsync(userId, userId);
        Assert.True(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_BlockedUser_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        
        _context.Blocks.Add(new Block { BlockerUserId = ownerId, BlockedUserId = viewerId });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.False(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_MissingSetting_DefaultsToConnectionsOnly_ReturnsTrueIfConnected()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.Connections.Add(new Connection { UserAId = ownerId, UserBId = viewerId });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.True(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_MissingSetting_DefaultsToConnectionsOnly_ReturnsFalseIfNotConnected()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.False(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_Everyone_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = ownerId, PresenceVisibility = PresenceVisibility.Everyone });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.True(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_Everyone_Blocked_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = ownerId, PresenceVisibility = PresenceVisibility.Everyone });
        _context.Blocks.Add(new Block { BlockerUserId = viewerId, BlockedUserId = ownerId });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.False(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_Nobody_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = ownerId, PresenceVisibility = PresenceVisibility.Nobody });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.False(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_Custom_Allowed_ReturnsTrue()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = ownerId, PresenceVisibility = PresenceVisibility.Custom });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = ownerId, TargetUserId = viewerId, IsAllowed = true });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.True(result);
    }

    [Fact]
    public async Task CanViewPresenceAsync_Custom_Denied_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _context.UserPresenceSettings.Add(new UserPresenceSetting { UserId = ownerId, PresenceVisibility = PresenceVisibility.Custom });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = ownerId, TargetUserId = viewerId, IsAllowed = false });
        await _context.SaveChangesAsync();

        var result = await _service.CanViewPresenceAsync(ownerId, viewerId);
        Assert.False(result);
    }
}

// Minimal mock DbContext for testing
public class ApplicationDbContextMock : Connect.Infrastructure.Persistence.ApplicationDbContext
{
    public ApplicationDbContextMock() 
        : base(new DbContextOptionsBuilder<Connect.Infrastructure.Persistence.ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options)
    {
    }
}
