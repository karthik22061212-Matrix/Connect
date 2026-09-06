using System;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.PresenceSettings.Commands.DeletePresenceVisibilityException;
using Connect.Application.Features.PresenceSettings.Commands.SetPresenceVisibilityException;
using Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;
using Connect.Application.Features.PresenceSettings.Queries.GetPresenceSettings;
using Connect.Application.Features.PresenceSettings.Queries.GetPresenceVisibilityExceptions;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Connect.Application.UnitTests.Services;

namespace Connect.Application.UnitTests.Features.PresenceSettings;

public class PresenceSettingsHandlerTests : IDisposable
{
    private readonly ApplicationDbContextMock _context;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    
    private readonly Guid _currentUserId = Guid.NewGuid();

    public PresenceSettingsHandlerTests()
    {
        _context = new ApplicationDbContextMock();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task UpdatePresenceSettings_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new UpdatePresenceSettingsCommandHandler(_context, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            handler.Handle(new UpdatePresenceSettingsCommand(PresenceVisibility.Everyone), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePresenceSettings_CreatesNewSetting_AndOnlyUpdatesCurrentUser()
    {
        var handler = new UpdatePresenceSettingsCommandHandler(_context, _currentUserServiceMock.Object);
        await handler.Handle(new UpdatePresenceSettingsCommand(PresenceVisibility.Nobody), CancellationToken.None);

        var settings = await _context.UserPresenceSettings.ToListAsync();
        Assert.Single(settings);
        Assert.Equal(_currentUserId, settings[0].UserId);
        Assert.Equal(PresenceVisibility.Nobody, settings[0].PresenceVisibility);
    }

    [Fact]
    public async Task GetPresenceSettings_MissingSetting_ReturnsConnectionsOnly_DoesNotInsertRow()
    {
        var handler = new GetPresenceSettingsQueryHandler(_context, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetPresenceSettingsQuery(), CancellationToken.None);

        Assert.Equal(PresenceVisibility.ConnectionsOnly, result);
        Assert.Empty(_context.UserPresenceSettings);
    }

    [Fact]
    public async Task SetPresenceVisibilityException_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new SetPresenceVisibilityExceptionCommandHandler(_context, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            handler.Handle(new SetPresenceVisibilityExceptionCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    [Fact]
    public async Task SetPresenceVisibilityException_OwnerCannotTargetSelf_ThrowsForbiddenAccessException()
    {
        var handler = new SetPresenceVisibilityExceptionCommandHandler(_context, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => 
            handler.Handle(new SetPresenceVisibilityExceptionCommand(_currentUserId, true), CancellationToken.None));
    }

    [Fact]
    public async Task SetPresenceVisibilityException_TargetUserMustExist_ThrowsNotFoundException()
    {
        var handler = new SetPresenceVisibilityExceptionCommandHandler(_context, _currentUserServiceMock.Object);
        
        // Non-existent target user
        await Assert.ThrowsAsync<NotFoundException>(() => 
            handler.Handle(new SetPresenceVisibilityExceptionCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    [Fact]
    public async Task SetPresenceVisibilityException_CreatesException_AndOnlySetsForCurrentUser()
    {
        var targetUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = targetUserId, UserId = targetUserId.ToString() });
        await _context.SaveChangesAsync();

        var handler = new SetPresenceVisibilityExceptionCommandHandler(_context, _currentUserServiceMock.Object);
        await handler.Handle(new SetPresenceVisibilityExceptionCommand(targetUserId, true), CancellationToken.None);

        var exceptions = await _context.PresenceVisibilityExceptions.ToListAsync();
        Assert.Single(exceptions);
        Assert.Equal(_currentUserId, exceptions[0].OwnerUserId);
        Assert.Equal(targetUserId, exceptions[0].TargetUserId);
        Assert.True(exceptions[0].IsAllowed);
    }

    [Fact]
    public async Task DeletePresenceVisibilityException_OnlyDeletesCurrentUserException()
    {
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = _currentUserId, TargetUserId = targetUserId, IsAllowed = true });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = otherUserId, TargetUserId = targetUserId, IsAllowed = true });
        await _context.SaveChangesAsync();

        var handler = new DeletePresenceVisibilityExceptionCommandHandler(_context, _currentUserServiceMock.Object);
        await handler.Handle(new DeletePresenceVisibilityExceptionCommand(targetUserId), CancellationToken.None);

        var exceptions = await _context.PresenceVisibilityExceptions.ToListAsync();
        Assert.Single(exceptions);
        Assert.Equal(otherUserId, exceptions[0].OwnerUserId); // other user's exception remains
    }

    [Fact]
    public async Task GetPresenceVisibilityExceptions_OnlyReturnsCurrentUserExceptions()
    {
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = _currentUserId, TargetUserId = targetUserId, IsAllowed = true });
        _context.PresenceVisibilityExceptions.Add(new PresenceVisibilityException { OwnerUserId = otherUserId, TargetUserId = targetUserId, IsAllowed = true });
        await _context.SaveChangesAsync();

        var handler = new GetPresenceVisibilityExceptionsQueryHandler(_context, _currentUserServiceMock.Object);
        var results = await handler.Handle(new GetPresenceVisibilityExceptionsQuery(), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(targetUserId, results[0].TargetUserId);
    }
}
