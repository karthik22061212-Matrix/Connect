using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Connections.Commands.DisconnectConnection;
using Connect.Domain.Entities;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.Features.Connections.Commands.DisconnectConnection;

public class DisconnectConnectionCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Connection>> _connectionsRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly List<Connection> _connections = new();

    public DisconnectConnectionCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);

        _connectionsRepoMock.Setup(r => r.Remove(It.IsAny<Connection>())).Callback<Connection>(c => _connections.Remove(c));
        
        _connectionsRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Connection, bool>> predicate, CancellationToken ct) => 
                _connections.FirstOrDefault(predicate.Compile()));

        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionsRepoMock.Object);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new DisconnectConnectionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            handler.Handle(new DisconnectConnectionCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TargetingSelf_ThrowsConflictException()
    {
        var handler = new DisconnectConnectionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<ConflictException>(() => 
            handler.Handle(new DisconnectConnectionCommand(_currentUserId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConnectionDoesNotExist_ThrowsNotFoundException()
    {
        var handler = new DisconnectConnectionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        
        await Assert.ThrowsAsync<NotFoundException>(() => 
            handler.Handle(new DisconnectConnectionCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConnectionExists_UserAIsCurrentUser_RemovesConnection()
    {
        var otherUserId = Guid.NewGuid();
        var userAId = _currentUserId.CompareTo(otherUserId) < 0 ? _currentUserId : otherUserId;
        var userBId = _currentUserId.CompareTo(otherUserId) < 0 ? otherUserId : _currentUserId;

        _connections.Add(new Connection { UserAId = userAId, UserBId = userBId });

        var handler = new DisconnectConnectionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new DisconnectConnectionCommand(otherUserId), CancellationToken.None);

        Assert.Empty(_connections);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherConnectionsExist_OnlyRemovesTargetConnection()
    {
        var targetUserId = Guid.NewGuid();
        var userAId1 = _currentUserId.CompareTo(targetUserId) < 0 ? _currentUserId : targetUserId;
        var userBId1 = _currentUserId.CompareTo(targetUserId) < 0 ? targetUserId : _currentUserId;

        var otherUserId = Guid.NewGuid();
        var userAId2 = _currentUserId.CompareTo(otherUserId) < 0 ? _currentUserId : otherUserId;
        var userBId2 = _currentUserId.CompareTo(otherUserId) < 0 ? otherUserId : _currentUserId;

        _connections.Add(new Connection { UserAId = userAId1, UserBId = userBId1 });
        _connections.Add(new Connection { UserAId = userAId2, UserBId = userBId2 });

        var handler = new DisconnectConnectionCommandHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new DisconnectConnectionCommand(targetUserId), CancellationToken.None);

        Assert.Single(_connections);
        Assert.Equal(userAId2, _connections[0].UserAId);
        Assert.Equal(userBId2, _connections[0].UserBId);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
