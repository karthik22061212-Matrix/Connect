using Connect.Application.Features.Account.Commands.PurgeOldCallHistory;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Connect.Infrastructure.Services;

public class CallHistoryPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallHistoryPurgeBackgroundService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    public CallHistoryPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CallHistoryPurgeBackgroundService> logger)
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
                var purgedCount = await mediator.Send(new PurgeOldCallHistoryCommand(), stoppingToken);
                _logger.LogInformation("Background call history purge completed. Purged {Count} records.", purgedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while running background call history purge service.");
            }
        }
    }
}
