using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Users.Queries.SearchUsers;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Users;

public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<IRepository<ConnectRequest>> _requestRepoMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public SearchUsersQueryHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);
    }

    [Fact]
    public async Task Handle_NoRelationship_ReturnsAvailable()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PhoneNumber = "1234567890",
            PresenceStatus = PresenceStatus.Online
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(_targetUserId, result.Id);
        Assert.Equal(RelationshipState.Available, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_OutgoingRequest_ReturnsPendingSent()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var requestId = Guid.NewGuid();
        var request = new ConnectRequest
        {
            Id = requestId,
            FromUserId = _currentUserId,
            ToUserId = _targetUserId,
            Status = ConnectRequestStatus.Pending
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest> { request });
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.PendingSent, result.RelationshipState);
        Assert.Equal(requestId, result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_IncomingRequest_ReturnsPendingReceived()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var requestId = Guid.NewGuid();
        var request = new ConnectRequest
        {
            Id = requestId,
            FromUserId = _targetUserId,
            ToUserId = _currentUserId,
            Status = ConnectRequestStatus.Pending
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest> { request });
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.PendingReceived, result.RelationshipState);
        Assert.Equal(requestId, result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_ActiveConnection_ReturnsConnected()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var minId = _currentUserId.CompareTo(_targetUserId) < 0 ? _currentUserId : _targetUserId;
        var maxId = _currentUserId.CompareTo(_targetUserId) < 0 ? _targetUserId : _currentUserId;

        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            UserAId = minId,
            UserBId = maxId
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection> { connection });
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.Connected, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_BlockerPerspective_ReturnsBlocked()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var block = new Block
        {
            Id = Guid.NewGuid(),
            BlockerUserId = _currentUserId,
            BlockedUserId = _targetUserId
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block> { block });

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.Blocked, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_BlockedPerspective_ReturnsAvailable()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var block = new Block
        {
            Id = Guid.NewGuid(),
            BlockerUserId = _targetUserId,
            BlockedUserId = _currentUserId
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block> { block });

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.Available, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Handle_UnauthenticatedSearch_ReturnsAvailable()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "target_user",
            Email = "target@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("target");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(RelationshipState.Available, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }
}
