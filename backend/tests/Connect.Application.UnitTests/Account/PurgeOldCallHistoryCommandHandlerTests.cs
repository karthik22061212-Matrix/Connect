using System.Linq.Expressions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Account.Commands.PurgeOldCallHistory;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Account;

public class PurgeOldCallHistoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Call>> _callRepoMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly PurgeOldCallHistoryCommandHandler _handler;

    public PurgeOldCallHistoryCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Calls).Returns(_callRepoMock.Object);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc));

        _handler = new PurgeOldCallHistoryCommandHandler(
            _unitOfWorkMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_CallsOlderThan90Days_PurgesExpiredCalls()
    {
        // Arrange
        var now = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        var oldCall1 = new Call { Id = Guid.NewGuid(), StartedAt = now.AddDays(-91) };
        var oldCall2 = new Call { Id = Guid.NewGuid(), StartedAt = now.AddDays(-100) };

        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { oldCall1, oldCall2 });

        // Act
        var result = await _handler.Handle(new PurgeOldCallHistoryCommand(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result);
        _callRepoMock.Verify(r => r.Remove(oldCall1), Times.Once);
        _callRepoMock.Verify(r => r.Remove(oldCall2), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCallsOlderThan90Days_ReturnsZero()
    {
        // Arrange
        _callRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call>());

        // Act
        var result = await _handler.Handle(new PurgeOldCallHistoryCommand(), CancellationToken.None);

        // Assert
        Assert.Equal(0, result);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
