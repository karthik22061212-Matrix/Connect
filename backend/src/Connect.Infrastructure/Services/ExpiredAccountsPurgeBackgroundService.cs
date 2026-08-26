using Connect.Application.Features.Account.Commands.PurgeExpiredAccounts;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Services;

public class ExpiredAccountsPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredAccountsPurgeBackgroundService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    public ExpiredAccountsPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredAccountsPurgeBackgroundService> logger)
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
                var purgedCount = await mediator.Send(new PurgeExpiredAccountsCommand(), stoppingToken);
                _logger.LogInformation("Background expired accounts purge completed. Purged {Count} accounts.", purgedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while running background expired accounts purge service.");
            }
        }
    }
}
