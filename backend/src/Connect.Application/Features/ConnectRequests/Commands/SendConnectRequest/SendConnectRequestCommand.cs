using Connect.Application.Features.ConnectRequests.Models;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;

public record SendConnectRequestCommand(
    Guid ToUserId
) : IRequest<ConnectRequestDto>;
