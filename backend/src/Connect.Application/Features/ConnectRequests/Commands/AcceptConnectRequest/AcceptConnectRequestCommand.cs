using Connect.Application.Features.ConnectRequests.Models;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;

public record AcceptConnectRequestCommand(
    Guid RequestId
) : IRequest<ConnectRequestDto>;
