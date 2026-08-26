using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Users.Queries.CheckUserIdAvailability;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Users;

public class CheckUserIdAvailabilityQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly CheckUserIdAvailabilityQueryHandler _handler;

    public CheckUserIdAvailabilityQueryHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _handler = new CheckUserIdAvailabilityQueryHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_AvailableUserId_ReturnsIsAvailableTrue()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var query = new CheckUserIdAvailabilityQuery("new_user");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Equal("new_user", result.UserId);
    }

    [Fact]
    public async Task Handle_TakenUserId_ReturnsIsAvailableFalse()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { new User { UserId = "existing_user" } });

        var query = new CheckUserIdAvailabilityQuery("EXISTING_USER");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsAvailable);
    }
}
