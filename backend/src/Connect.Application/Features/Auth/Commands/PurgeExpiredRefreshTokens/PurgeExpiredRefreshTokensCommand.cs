using MediatR;

namespace Connect.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;

public record PurgeExpiredRefreshTokensCommand : IRequest<int>;
