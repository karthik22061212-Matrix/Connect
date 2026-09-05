using System;
using MediatR;

namespace Connect.Application.Features.PresenceSettings.Commands.SetPresenceVisibilityException;

public record SetPresenceVisibilityExceptionCommand(Guid TargetUserId, bool IsAllowed) : IRequest;
