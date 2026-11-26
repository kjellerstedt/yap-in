using YapIn.Domain.Models;

namespace YapIn.Domain.Interfaces;

public interface IInboxService
{
    /// <summary>The cached inbox for <paramref name="query"/>, built first if it has never been asked for.</summary>
    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be searched.</exception>
    Task<Inbox> GetAsync(InboxQuery query, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be searched.</exception>
    Task<Inbox> RebuildAsync(InboxQuery query, CancellationToken cancellationToken);

    /// <summary>Every query asked for so far, so a refresh can keep each one current.</summary>
    IReadOnlyCollection<InboxQuery> CachedQueries { get; }
}
