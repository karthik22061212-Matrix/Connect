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
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly ReportUserCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _reportedUserId = Guid.NewGuid();

    public ReportUserCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Reports).Returns(_reportRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new ReportUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
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
}
