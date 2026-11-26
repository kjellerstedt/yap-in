using YapIn.Domain.Models;

namespace YapIn.Domain.Interfaces;

/// <summary>GitHub's search, as the pull requests that involve you. Throws ExternalToolFailedException when its program fails.</summary>
public interface IPullRequestSearch
{
    Task<Inbox> BuildInboxAsync(InboxQuery query, CancellationToken cancellationToken);
}
