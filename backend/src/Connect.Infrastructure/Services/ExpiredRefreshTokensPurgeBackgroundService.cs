using Connect.Application.Features.Auth.Commands.PurgeExpiredRefreshTokens;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Services;

public class ExpiredRefreshTokensPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredRefreshTokensPurgeBackgroundService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    public ExpiredRefreshTokensPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredRefreshTokensPurgeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                var purgedCount = await mediator.Send(new PurgeExpiredRefreshTokensCommand(), stoppingToken);
                _logger.LogInformation("Background expired refresh tokens purge completed. Purged {Count} tokens.", purgedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while running background expired refresh tokens purge service.");
            }
        }
    }
}
