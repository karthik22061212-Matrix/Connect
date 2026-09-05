using System;
using MediatR;

namespace Connect.Application.Features.PresenceSettings.Commands.DeletePresenceVisibilityException;

public record DeletePresenceVisibilityExceptionCommand(Guid TargetUserId) : IRequest;
