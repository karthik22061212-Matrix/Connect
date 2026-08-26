using MediatR;

namespace Connect.Application.Features.Account.Commands.PurgeOldCallHistory;

public record PurgeOldCallHistoryCommand() : IRequest<int>;
