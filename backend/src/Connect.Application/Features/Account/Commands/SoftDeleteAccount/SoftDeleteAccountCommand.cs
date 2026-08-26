using MediatR;

namespace Connect.Application.Features.Account.Commands.SoftDeleteAccount;

public record SoftDeleteAccountCommand() : IRequest<bool>;
