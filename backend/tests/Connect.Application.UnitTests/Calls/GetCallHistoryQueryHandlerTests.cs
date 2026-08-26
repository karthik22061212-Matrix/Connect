using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Queries.GetCallHistory;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Calls;

public class GetCallHistoryQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly GetCallHistoryQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public GetCallHistoryQueryHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 8, 26));

        _handler = new GetCallHistoryQueryHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedCallHistory_FilteringRetentionCutoff()
    {
        var now = new DateTime(2026, 8, 26);
        var recentCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userId,
            CalleeId = _otherUserId,
            Status = CallStatus.Completed,
            StartedAt = now.AddDays(-10)
        };

        var oldCall = new Call
        {
            Id = Guid.NewGuid(),
            CallerId = _userId,
            CalleeId = _otherUserId,
            Status = CallStatus.Completed,
            StartedAt = now.AddDays(-100) // Exceeds 90 days
        };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { recentCall, oldCall });

        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>
            {
                new User { Id = _userId, UserId = "user1" },
                new User { Id = _otherUserId, UserId = "user2" }
            });

        var query = new GetCallHistoryQuery(PageNumber: 1, PageSize: 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(recentCall.Id, result.Items[0].Id);
        Assert.True(result.Items[0].IsOutgoing);
    }
}
