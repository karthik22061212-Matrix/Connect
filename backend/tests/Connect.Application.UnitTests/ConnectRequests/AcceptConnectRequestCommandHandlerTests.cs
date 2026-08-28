using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.ConnectRequests;

public class AcceptConnectRequestCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<ConnectRequest>> _requestRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly AcceptConnectRequestCommandHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _senderUserId = Guid.NewGuid();
    private readonly Guid _requestId = Guid.NewGuid();

    public AcceptConnectRequestCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_requestRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);

        _currentUserServiceMock.Setup(c => c.UserId).Returns(_currentUserId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new AcceptConnectRequestCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ValidPendingRequest_AcceptsAndEnforcesUserAIdLessThanUserBId()
    {
        // Arrange
        var request = new ConnectRequest
        {
            Id = _requestId,
            FromUserId = _senderUserId,
            ToUserId = _currentUserId,
            Status = ConnectRequestStatus.Pending
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _connectionRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var command = new AcceptConnectRequestCommand(_requestId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ConnectRequestStatus.Accepted, result.Status);

        var expectedUserAId = _senderUserId.CompareTo(_currentUserId) < 0 ? _senderUserId : _currentUserId;
        var expectedUserBId = _senderUserId.CompareTo(_currentUserId) < 0 ? _currentUserId : _senderUserId;

        _connectionRepoMock.Verify(r => r.Add(It.Is<Connection>(c =>
            c.UserAId == expectedUserAId && c.UserBId == expectedUserBId)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RequestSentToSomeoneElse_ThrowsForbiddenAccessException()
    {
        // Arrange
        var request = new ConnectRequest
        {
            Id = _requestId,
            FromUserId = _senderUserId,
            ToUserId = Guid.NewGuid(), // Other user
            Status = ConnectRequestStatus.Pending
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(_requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new AcceptConnectRequestCommand(_requestId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
