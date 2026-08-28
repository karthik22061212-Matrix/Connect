using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Blocking.Commands.BlockUser;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Blocking;

public class BlockUserCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly BlockUserCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public BlockUserCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new BlockUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_SelfBlock_ThrowsConflictException()
    {
        var command = new BlockUserCommand(_userId);
        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidBlock_AddsBlockRecord()
    {
        var targetUser = new User { Id = _targetUserId, UserId = "target" };
        _userRepoMock.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blockRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Block, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new BlockUserCommand(_targetUserId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result);
        _blockRepoMock.Verify(r => r.Add(It.Is<Block>(b => b.BlockerUserId == _userId && b.BlockedUserId == _targetUserId)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
