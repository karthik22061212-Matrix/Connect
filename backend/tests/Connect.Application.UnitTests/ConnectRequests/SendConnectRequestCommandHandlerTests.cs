using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.ConnectRequests;

public class SendConnectRequestCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<ConnectRequest>> _requestRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly SendConnectRequestCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public SendConnectRequestCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new SendConnectRequestCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesConnectRequest()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _targetUserId, UserId = "target_user" });
        _blockRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Block?)null);
        _connectionRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ConnectRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new SendConnectRequestCommand(_targetUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_currentUserId, result.FromUserId);
        Assert.Equal(_targetUserId, result.ToUserId);
        Assert.Equal(ConnectRequestStatus.Pending, result.Status);

        _requestRepoMock.Verify(r => r.Add(It.IsAny<ConnectRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendToSelf_ThrowsConflictException()
    {
        // Arrange
        var command = new SendConnectRequestCommand(_currentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SenderBlockedByRecipient_ReturnsSyntheticPendingDtoWithoutSaving()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _targetUserId, UserId = "target_user" });

        // Target user (recipient) blocked the current user (sender)
        var blockRecord = new Block
        {
            BlockerUserId = _targetUserId,
            BlockedUserId = _currentUserId
        };
        _blockRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockRecord);

        var command = new SendConnectRequestCommand(_targetUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_currentUserId, result.FromUserId);
        Assert.Equal(_targetUserId, result.ToUserId);
        Assert.Equal(ConnectRequestStatus.Pending, result.Status);

        _requestRepoMock.Verify(r => r.Add(It.IsAny<ConnectRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SenderBlockedRecipient_ThrowsConflictException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _targetUserId, UserId = "target_user" });

        // Current user (sender) blocked the target user (recipient)
        var blockRecord = new Block
        {
            BlockerUserId = _currentUserId,
            BlockedUserId = _targetUserId
        };
        _blockRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockRecord);

        var command = new SendConnectRequestCommand(_targetUserId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("You have blocked this user", ex.Message);
        _requestRepoMock.Verify(r => r.Add(It.IsAny<ConnectRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyConnected_ThrowsConflictException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _targetUserId, UserId = "target_user" });
        _blockRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Block?)null);
        _connectionRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new SendConnectRequestCommand(_targetUserId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("already connected", ex.Message);
    }

    [Fact]
    public async Task Handle_PendingRequestExists_ThrowsConflictException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _targetUserId, UserId = "target_user" });
        _blockRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Block?)null);
        _connectionRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _requestRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ConnectRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new SendConnectRequestCommand(_targetUserId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("pending connect request already exists", ex.Message);
    }
}
