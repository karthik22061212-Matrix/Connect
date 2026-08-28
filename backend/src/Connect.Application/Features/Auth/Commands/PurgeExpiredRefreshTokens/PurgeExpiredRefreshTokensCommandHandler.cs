using Connect.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;

public class PurgeExpiredRefreshTokensCommandHandler : IRequestHandler<PurgeExpiredRefreshTokensCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PurgeExpiredRefreshTokensCommandHandler(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<int> Handle(PurgeExpiredRefreshTokensCommand request, CancellationToken cancellationToken)
    {
        var cutoff = _dateTimeProvider.UtcNow.AddDays(-7);
        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count == 0)
        {
            return 0;
        }

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync(cancellationToken);
        return expiredTokens.Count;
    }
}
