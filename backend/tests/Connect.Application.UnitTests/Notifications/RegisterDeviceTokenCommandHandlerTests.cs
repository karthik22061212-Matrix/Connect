using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Notifications.Commands.RegisterDeviceToken;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Moq;

namespace Connect.Application.UnitTests.Notifications;

public class RegisterDeviceTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<DeviceToken>> _deviceTokenRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly RegisterDeviceTokenCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public RegisterDeviceTokenCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.DeviceTokens).Returns(_deviceTokenRepoMock.Object);
        _currentUserServiceMock.Setup(c => c.UserId).Returns(_userId);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new RegisterDeviceTokenCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_NewToken_AddsDeviceToken()
    {
        _deviceTokenRepoMock.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceToken>());

        var command = new RegisterDeviceTokenCommand("fcm_token_123", DevicePlatform.Web);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result);
        _deviceTokenRepoMock.Verify(r => r.Add(It.Is<DeviceToken>(t => t.Token == "fcm_token_123" && t.UserId == _userId)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
