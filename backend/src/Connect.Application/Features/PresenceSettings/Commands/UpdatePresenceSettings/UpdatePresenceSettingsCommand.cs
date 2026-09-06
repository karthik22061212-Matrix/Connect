using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;

public record UpdatePresenceSettingsCommand(PresenceVisibility Visibility) : IRequest;
