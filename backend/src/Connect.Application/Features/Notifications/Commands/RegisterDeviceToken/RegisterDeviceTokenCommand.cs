using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Notifications.Commands.RegisterDeviceToken;

public record RegisterDeviceTokenCommand(
    string Token,
    DevicePlatform Platform
) : IRequest<bool>;
