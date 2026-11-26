using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;

namespace YapIn.Web.Inbox;

/// <summary>Rebuilds every cached inbox each minute. Each pass starts after the last finishes, so passes never overlap.</summary>
public sealed partial class InboxRefreshService(IInboxService inboxService, ILogger<InboxRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var query in inboxService.CachedQueries)
            {
                try
                {
                    await inboxService.RebuildAsync(query, stoppingToken);
                }
                catch (ExternalToolFailedException exception)
                {
                    LogRefreshFailed(logger, query.State, query.Lookback.Value, exception.Message);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background refresh of the {State} inbox ({LookbackDays} days) failed: {Reason}")]
    private static partial void LogRefreshFailed(ILogger logger, Domain.Models.InboxState state, int lookbackDays, string reason);
}
