using System;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.PresenceSettings;

public class UpdatePresenceSettingsCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly UpdatePresenceSettingsCommandHandler _handler;

    public UpdatePresenceSettingsCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _handler = new UpdatePresenceSettingsCommandHandler(_contextMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var command = new UpdatePresenceSettingsCommand(PresenceVisibility.Custom);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
