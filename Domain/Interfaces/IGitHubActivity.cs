using YapIn.Domain.Models;

namespace YapIn.Domain.Interfaces;

/// <summary>A cheap way to tell that something happened on GitHub, so the inbox is refreshed when it matters and not on a timer.</summary>
public interface IGitHubActivity
{
    /// <exception cref="Exceptions.ExternalToolFailedException">When GitHub cannot be asked.</exception>
    Task<ActivityCheck> CheckForActivityAsync(CancellationToken cancellationToken);
}
