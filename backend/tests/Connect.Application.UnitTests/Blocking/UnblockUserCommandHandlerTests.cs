using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Blocking.Commands.UnblockUser;
using Connect.Domain.Entities;
using Moq;

namespace Connect.Application.UnitTests.Blocking;

public class UnblockUserCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Block>> _blockRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly UnblockUserCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    public UnblockUserCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Blocks).Returns(_blockRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);

        _handler = new UnblockUserCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingBlock_RemovesBlockRecord()
    {
        var existingBlock = new Block { Id = Guid.NewGuid(), BlockerUserId = _userId, BlockedUserId = _targetUserId };
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block> { existingBlock });

        var command = new UnblockUserCommand(_targetUserId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result);
        _blockRepoMock.Verify(r => r.Remove(existingBlock), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingBlock_ThrowsNotFoundException()
    {
        _blockRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Block>());

        var command = new UnblockUserCommand(_targetUserId);
        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
