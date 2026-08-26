using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using MediatR;

namespace Connect.Application.Features.Notifications.Commands.RegisterDeviceToken;

public class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterDeviceTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var existingTokens = await _unitOfWork.DeviceTokens.ListAsync(cancellationToken);
        var deviceToken = existingTokens.FirstOrDefault(t => t.Token == request.Token);

        var now = _dateTimeProvider.UtcNow;

        if (deviceToken != null)
        {
            deviceToken.UserId = currentUserId.Value;
            deviceToken.Platform = request.Platform;
            deviceToken.UpdatedAt = now;
        }
        else
        {
            deviceToken = new DeviceToken
            {
                UserId = currentUserId.Value,
                Token = request.Token,
                Platform = request.Platform,
                CreatedAt = now,
                UpdatedAt = now
            };
            _unitOfWork.DeviceTokens.Add(deviceToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
