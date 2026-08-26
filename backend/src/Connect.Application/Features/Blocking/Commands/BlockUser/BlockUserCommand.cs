using MediatR;

namespace Connect.Application.Features.Blocking.Commands.BlockUser;

public record BlockUserCommand(
    Guid UserIdToBlock
) : IRequest<bool>;
