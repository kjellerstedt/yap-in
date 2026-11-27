using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

public interface IInboxService
{
    /// <summary>The cached inbox for <paramref name="query"/>, built first if it has never been asked for.</summary>
    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be searched.</exception>
    Task<Inbox> GetAsync(InboxQuery query, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be searched.</exception>
    Task<Inbox> RebuildAsync(InboxQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Brings every cached open inbox up to date without searching: its open pull requests are fetched again, and any of
    /// <paramref name="notifiedPullRequests"/> that is open and not in it yet is added.
    /// </summary>
    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be asked.</exception>
    Task RefreshOpenInboxesAsync(IReadOnlyList<PullRequestUrl> notifiedPullRequests, CancellationToken cancellationToken);

    /// <summary>Every query asked for so far, so a refresh can keep each one current.</summary>
    IReadOnlyCollection<InboxQuery> CachedQueries { get; }
}
