using MediatR;

namespace Connect.Application.Features.Connections.Commands.DisconnectConnection;

public record DisconnectConnectionCommand(Guid TargetUserId) : IRequest;
