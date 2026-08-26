using Connect.Application.Features.ConnectRequests.Models;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Commands.DeclineConnectRequest;

public record DeclineConnectRequestCommand(
    Guid RequestId
) : IRequest<ConnectRequestDto>;
