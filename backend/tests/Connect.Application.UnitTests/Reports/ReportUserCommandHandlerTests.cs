using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Reports.Commands.ReportUser;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Reports;

public class ReportUserCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Report>> _reportRepoMock = new();
    private readonly Mock<IRepository<Connection>> _connectionRepoMock = new();
    private readonly Mock<IRepository<ConnectRequest>> _connectRequestRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<ICallRealtimeNotifier> _callRealtimeNotifierMock = new();
    private readonly ReportUserCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _reportedUserId = Guid.NewGuid();

    public ReportUserCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Reports).Returns(_reportRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Connections).Returns(_connectionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ConnectRequests).Returns(_connectRequestRepoMock.Object);
        _connectRequestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new ReportUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object,
            _callRealtimeNotifierMock.Object);
    }

    [Fact]
    public async Task Handle_SelfReport_ThrowsConflictException()
    {
        var command = new ReportUserCommand(_userId, "Harassment", "Note");
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidReport_CreatesReportRecord()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var command = new ReportUserCommand(_reportedUserId, "Spam", "Spamming messages");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        _reportRepoMock.Verify(r => r.Add(It.Is<Report>(rep =>
            rep.ReporterUserId == _userId &&
            rep.ReportedUserId == _reportedUserId &&
            rep.Reason == "Spam" &&
            rep.Status == ReportStatus.Open
        )), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ReportUserCommand(_reportedUserId, "Spam", "Notes");
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExistingOpenReport_ReturnsExistingIdWithoutCreatingNewReport()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var existingReportId = Guid.NewGuid();
        var existingReport = new Report
        {
            Id = existingReportId,
            ReporterUserId = _userId,
            ReportedUserId = _reportedUserId,
            Reason = "Harassment",
            Note = "Previous note",
            Status = ReportStatus.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _reportRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Report, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReport);

        var command = new ReportUserCommand(_reportedUserId, "Spam", "Duplicate report attempt");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(existingReportId, result);
        _reportRepoMock.Verify(r => r.Add(It.IsAny<Report>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingResolvedReport_CreatesNewReportRecord()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        _reportRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Report, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Report?)null);

        var command = new ReportUserCommand(_reportedUserId, "Spam", "New report after resolved");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        _reportRepoMock.Verify(r => r.Add(It.Is<Report>(rep =>
            rep.ReporterUserId == _userId &&
            rep.ReportedUserId == _reportedUserId &&
            rep.Reason == "Spam" &&
            rep.Status == ReportStatus.Open
        )), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidReport_ExecutesAllCascades()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var minId = _userId.CompareTo(_reportedUserId) < 0 ? _userId : _reportedUserId;
        var maxId = _userId.CompareTo(_reportedUserId) < 0 ? _reportedUserId : _userId;

        var existingConnection = new Connection
        {
            Id = Guid.NewGuid(),
            UserAId = minId,
            UserBId = maxId
        };

        _connectionRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConnection);

        var request1 = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _userId,
            ToUserId = _reportedUserId,
            Status = ConnectRequestStatus.Pending
        };

        var request2 = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _reportedUserId,
            ToUserId = _userId,
            Status = ConnectRequestStatus.Pending
        };

        var thirdPartyUserId = Guid.NewGuid();
        var thirdPartyRequest = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _reportedUserId,
            ToUserId = thirdPartyUserId,
            Status = ConnectRequestStatus.Pending
        };

        _connectRequestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest> { request1, request2, thirdPartyRequest });

        var command = new ReportUserCommand(_reportedUserId, "Harassment", "Sever all ties");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);

        // Cascade 1: Connection removed
        _connectionRepoMock.Verify(r => r.Remove(existingConnection), Times.Once);

        // Cascade 2: Requests marked Declined with timestamp (third-party untouched)
        Assert.Equal(ConnectRequestStatus.Declined, request1.Status);
        Assert.Equal(ConnectRequestStatus.Declined, request2.Status);
        Assert.NotNull(request1.RespondedAt);
        Assert.NotNull(request2.RespondedAt);
        Assert.Equal(ConnectRequestStatus.Pending, thirdPartyRequest.Status);
        Assert.Null(thirdPartyRequest.RespondedAt);

        // Cascade 3: Report persisted
        _reportRepoMock.Verify(r => r.Add(It.Is<Report>(rep =>
            rep.ReporterUserId == _userId &&
            rep.ReportedUserId == _reportedUserId &&
            rep.Reason == "Harassment" &&
            rep.Status == ReportStatus.Open
        )), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Cascade 4: Terminate calls
        _callRealtimeNotifierMock.Verify(n => n.TerminateActiveCallsBetweenUsersAsync(
            _userId, _reportedUserId, "UserReported", It.IsAny<CancellationToken>()), Times.Once);

        // Notification of connection removal
        _callRealtimeNotifierMock.Verify(n => n.NotifyConnectionRemovedAsync(
            _userId, _reportedUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidReport_NoPriorRelationship_ExecutesSafely()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        _connectionRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        _connectRequestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest>());

        var command = new ReportUserCommand(_reportedUserId, "Spam", "No connection or request");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        _connectionRepoMock.Verify(r => r.Remove(It.IsAny<Connection>()), Times.Never);
        _reportRepoMock.Verify(r => r.Add(It.IsAny<Report>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _callRealtimeNotifierMock.Verify(n => n.TerminateActiveCallsBetweenUsersAsync(
            _userId, _reportedUserId, "UserReported", It.IsAny<CancellationToken>()), Times.Once);
        _callRealtimeNotifierMock.Verify(n => n.NotifyConnectionRemovedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidReport_PendingRequestFromBOnly_DeclinesRequest()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        _connectionRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var requestFromB = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _reportedUserId,
            ToUserId = _userId,
            Status = ConnectRequestStatus.Pending
        };

        _connectRequestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest> { requestFromB });

        var command = new ReportUserCommand(_reportedUserId, "Harassment", "Pending from B");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        Assert.Equal(ConnectRequestStatus.Declined, requestFromB.Status);
        Assert.NotNull(requestFromB.RespondedAt);
        _reportRepoMock.Verify(r => r.Add(It.IsAny<Report>()), Times.Once);
        _callRealtimeNotifierMock.Verify(n => n.TerminateActiveCallsBetweenUsersAsync(
            _userId, _reportedUserId, "UserReported", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidReport_PendingRequestFromAOnly_DeclinesRequest()
    {
        var reportedUser = new User { Id = _reportedUserId, UserId = "bad_user" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        _connectionRepoMock.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Connection, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var requestFromA = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = _userId,
            ToUserId = _reportedUserId,
            Status = ConnectRequestStatus.Pending
        };

        _connectRequestRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectRequest> { requestFromA });

        var command = new ReportUserCommand(_reportedUserId, "Harassment", "Pending from A");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        Assert.Equal(ConnectRequestStatus.Declined, requestFromA.Status);
        Assert.NotNull(requestFromA.RespondedAt);
        _reportRepoMock.Verify(r => r.Add(It.IsAny<Report>()), Times.Once);
        _callRealtimeNotifierMock.Verify(n => n.TerminateActiveCallsBetweenUsersAsync(
            _userId, _reportedUserId, "UserReported", It.IsAny<CancellationToken>()), Times.Once);
    }
}
