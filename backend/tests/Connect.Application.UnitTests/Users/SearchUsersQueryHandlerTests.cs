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
    private readonly Mock<IRepository<Report>> _reportRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public SearchUsersQueryHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Reports).Returns(_reportRepoMock.Object);

        _reportRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Report>());

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

    [Fact]
    public async Task Should_Return_Blocked_User_Tagged_As_Blocked_When_Current_User_Is_Blocker()
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
        Assert.Equal(_targetUserId, result.Id);
        Assert.Equal(RelationshipState.Blocked, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Should_Return_Blocker_Tagged_As_Available_When_Current_User_Is_Blocked()
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
        Assert.Equal(_targetUserId, result.Id);
        Assert.Equal(RelationshipState.Available, result.RelationshipState);
        Assert.Null(result.PendingRequestId);
    }

    [Fact]
    public async Task Should_Exclude_Users_In_Mutual_Report_Relationship()
    {
        var reportedByMeUser = new User
        {
            Id = Guid.NewGuid(),
            UserId = "reported_by_me",
            Email = "reported_by_me@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var whoReportedMeUser = new User
        {
            Id = Guid.NewGuid(),
            UserId = "who_reported_me",
            Email = "who_reported_me@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var normalUser = new User
        {
            Id = _targetUserId,
            UserId = "normal_user",
            Email = "normal@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var reports = new List<Report>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReporterUserId = _currentUserId,
                ReportedUserId = reportedByMeUser.Id,
                Reason = "Spam"
            },
            new()
            {
                Id = Guid.NewGuid(),
                ReporterUserId = whoReportedMeUser.Id,
                ReportedUserId = _currentUserId,
                Reason = "Harassment"
            }
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { reportedByMeUser, whoReportedMeUser, normalUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());
        _reportRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);

        // Search for reported by me
        var queryReported = new SearchUsersQuery("reported_by_me");
        var resultsReported = (await handler.Handle(queryReported, CancellationToken.None)).ToList();
        Assert.Empty(resultsReported);

        // Search for user who reported current user
        var queryReporter = new SearchUsersQuery("who_reported_me");
        var resultsReporter = (await handler.Handle(queryReporter, CancellationToken.None)).ToList();
        Assert.Empty(resultsReporter);

        // Normal user still found
        var queryNormal = new SearchUsersQuery("normal");
        var resultsNormal = (await handler.Handle(queryNormal, CancellationToken.None)).ToList();
        Assert.Single(resultsNormal);
        Assert.Equal(_targetUserId, resultsNormal.First().Id);
    }

    [Fact]
    public async Task Should_Exclude_Self_And_SoftDeleted_Users()
    {
        var currentUser = new User
        {
            Id = _currentUserId,
            UserId = "my_username",
            Email = "me@example.com",
            IsDeleted = false
        };

        var softDeletedUser = new User
        {
            Id = Guid.NewGuid(),
            UserId = "deleted_user",
            Email = "deleted@example.com",
            IsDeleted = true
        };

        var activeUser = new User
        {
            Id = _targetUserId,
            UserId = "active_user",
            Email = "active@example.com",
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { currentUser, softDeletedUser, activeUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);

        // Self search excluded
        var querySelf = new SearchUsersQuery("my_username");
        var resultsSelf = (await handler.Handle(querySelf, CancellationToken.None)).ToList();
        Assert.Empty(resultsSelf);

        // Soft-deleted user excluded
        var queryDeleted = new SearchUsersQuery("deleted_user");
        var resultsDeleted = (await handler.Handle(queryDeleted, CancellationToken.None)).ToList();
        Assert.Empty(resultsDeleted);

        // Active user found
        var queryActive = new SearchUsersQuery("active_user");
        var resultsActive = (await handler.Handle(queryActive, CancellationToken.None)).ToList();
        Assert.Single(resultsActive);
        Assert.Equal(_targetUserId, resultsActive.First().Id);
    }

    [Fact]
    public async Task Should_Return_Blocked_When_Mutual_Block_Exists()
    {
        var targetUser = new User
        {
            Id = _targetUserId,
            UserId = "mutual_block_user",
            Email = "mutual@example.com",
            PresenceStatus = PresenceStatus.Online
        };

        var blocks = new List<Block>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BlockerUserId = _currentUserId,
                BlockedUserId = _targetUserId
            },
            new()
            {
                Id = Guid.NewGuid(),
                BlockerUserId = _targetUserId,
                BlockedUserId = _currentUserId
            }
        };

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { targetUser });
        _connectionRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Connection>());
        _requestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(blocks);

        var handler = new SearchUsersQueryHandler(_unitOfWorkMock.Object, _currentUserServiceMock.Object);
        var query = new SearchUsersQuery("mutual");

        var results = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(results);
        var result = results.First();
        Assert.Equal(_targetUserId, result.Id);
        Assert.Equal(RelationshipState.Blocked, result.RelationshipState);
    }
}
