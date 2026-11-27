using System.Collections.Concurrent;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Services;

/// <summary>
/// The last inbox built for each query asked for. A background refresh keeps every one current, so a cached inbox is
/// fresh enough to serve straight away.
/// </summary>
internal sealed class InboxService(IPullRequestSearch pullRequestSearch, TimeProvider timeProvider) : IInboxService
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

    public async Task RefreshOpenInboxesAsync(IReadOnlyList<PullRequestUrl> notifiedPullRequests, CancellationToken cancellationToken)
    {
        foreach (var (query, cachedInbox) in inboxByQuery.Where(entry => entry.Key.State == InboxState.Open).ToList())
        {
            var cachedUrls = cachedInbox.PullRequests.Select(pullRequest => pullRequest.Url.Value).ToHashSet();
            var unseenUrls = notifiedPullRequests.DistinctBy(url => url.Value).Where(url => cachedUrls.Contains(url.Value) == false).ToList();
            var openPullRequests = cachedInbox.PullRequests.Where(pullRequest => pullRequest.State == PullRequestState.Open).ToList();

            var refreshed = (await pullRequestSearch.RefreshPullRequestsAsync(openPullRequests, cancellationToken)).ToDictionary(pullRequest => pullRequest.Id);
            // Only open ones: a notification about an old merged pull request is not something the open view lists.
            var added = (await pullRequestSearch.FetchPullRequestsAsync(unseenUrls, cancellationToken)).Where(pullRequest => pullRequest.State == PullRequestState.Open);

            var pullRequests = cachedInbox.PullRequests
                .Select(pullRequest => refreshed.GetValueOrDefault(pullRequest.Id, pullRequest))
                .Concat(added)
                .ToList();
            // A rebuild that finished meanwhile is newer than what this started from, so it wins.
            inboxByQuery.TryUpdate(query, new Inbox(cachedInbox.Viewer, timeProvider.GetUtcNow(), pullRequests), cachedInbox);
        }
    }
}
