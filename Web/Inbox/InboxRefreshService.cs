using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;

namespace YapIn.Web.Inbox;

/// <summary>
/// Keeps every cached inbox current. Each minute it asks GitHub's activity feeds whether anything happened and, if so, fetches
/// the open pull requests again; every five minutes it rebuilds each inbox with the full search, which is the only way
/// to find a pull request that newly involves you. It is one loop, so passes never overlap.
/// </summary>
public sealed partial class InboxRefreshService(
    IInboxService inboxService, IGitHubActivity gitHubActivity, TimeProvider timeProvider, ILogger<InboxRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan FullRebuildInterval = TimeSpan.FromMinutes(5);

    private DateTimeOffset nextFullRebuildAt = timeProvider.GetUtcNow() + FullRebuildInterval;

    /// <summary>How long to wait before the next pass: at least a minute, longer when GitHub asks for it.</summary>
    internal TimeSpan PollInterval { get; private set; } = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested == false)
        {
            await Task.Delay(PollInterval, timeProvider, stoppingToken);
            await RunPassAsync(stoppingToken);
        }
    }

    internal async Task RunPassAsync(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() >= nextFullRebuildAt)
        {
            await RebuildAllAsync(cancellationToken);
            nextFullRebuildAt = timeProvider.GetUtcNow() + FullRebuildInterval;
            return;
        }
        await RefreshOnActivityAsync(cancellationToken);
    }

    private async Task RebuildAllAsync(CancellationToken cancellationToken)
    {
        foreach (var query in inboxService.CachedQueries)
        {
            try
            {
                await inboxService.RebuildAsync(query, cancellationToken);
            }
            catch (ExternalToolFailedException exception)
            {
                LogRefreshFailed(logger, query.State, query.Lookback.Value, exception.Message);
            }
        }
    }

    private async Task RefreshOnActivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var activity = await gitHubActivity.CheckForActivityAsync(cancellationToken);
            PollInterval = activity.PollInterval;
            if (activity.HasChanged) await inboxService.RefreshOpenInboxesAsync(activity.NotifiedPullRequests, cancellationToken);
        }
        catch (ExternalToolFailedException exception)
        {
            LogActivityRefreshFailed(logger, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background refresh of the {State} inbox ({LookbackDays} days) failed: {Reason}")]
    private static partial void LogRefreshFailed(ILogger logger, Domain.Models.InboxState state, int lookbackDays, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking GitHub for activity failed: {Reason}")]
    private static partial void LogActivityRefreshFailed(ILogger logger, string reason);
}
