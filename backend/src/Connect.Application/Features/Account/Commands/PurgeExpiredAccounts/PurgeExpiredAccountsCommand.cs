using MediatR;

namespace Connect.Application.Features.Account.Commands.PurgeExpiredAccounts;

public record PurgeExpiredAccountsCommand() : IRequest<int>;
