using System.Collections.Concurrent;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;

namespace YapIn.Domain.Services;

/// <summary>
/// The last inbox built for each query asked for. A background refresh keeps every one current, so a cached inbox is
/// fresh enough to serve straight away.
/// </summary>
internal sealed class InboxService(IPullRequestSearch pullRequestSearch) : IInboxService
{
    private readonly ConcurrentDictionary<InboxQuery, Inbox> inboxByQuery = new();

    public IReadOnlyCollection<InboxQuery> CachedQueries => inboxByQuery.Keys.ToList();

    public async Task<Inbox> GetAsync(InboxQuery query, CancellationToken cancellationToken) =>
        inboxByQuery.TryGetValue(query, out var inbox) ? inbox : await RebuildAsync(query, cancellationToken);

    public async Task<Inbox> RebuildAsync(InboxQuery query, CancellationToken cancellationToken)
    {
        var inbox = await pullRequestSearch.BuildInboxAsync(query, cancellationToken);
        inboxByQuery[query] = inbox;
        return inbox;
    }
}
