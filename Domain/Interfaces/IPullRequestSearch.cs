using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

/// <summary>GitHub's search, as the pull requests that involve you. Throws ExternalToolFailedException when its program fails.</summary>
public interface IPullRequestSearch
{
    Task<Inbox> BuildInboxAsync(InboxQuery query, CancellationToken cancellationToken);

    /// <summary>The same pull requests as they are now, each keeping its relations. One GitHub no longer returns is left out.</summary>
    Task<IReadOnlyList<PullRequest>> RefreshPullRequestsAsync(IReadOnlyList<PullRequest> pullRequests, CancellationToken cancellationToken);

    /// <summary>
    /// The pull requests at these addresses. Their relations are worked out from what each says about you, and a mention is not
    /// visible there, so a pull request you were only mentioned in shows as involved until the inbox is next built.
    /// </summary>
    Task<IReadOnlyList<PullRequest>> FetchPullRequestsAsync(IReadOnlyList<PullRequestUrl> urls, CancellationToken cancellationToken);
}
