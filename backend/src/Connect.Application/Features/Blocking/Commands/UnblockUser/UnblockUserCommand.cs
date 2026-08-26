using MediatR;

namespace Connect.Application.Features.Blocking.Commands.UnblockUser;

public record UnblockUserCommand(
    Guid UserIdToUnblock
) : IRequest<bool>;
