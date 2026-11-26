using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

public interface IReviewService
{
    /// <exception cref="Exceptions.NoLocalCloneException">When the session host has no workspace and there is no local clone.</exception>
    /// <exception cref="Exceptions.NoSessionHostException">When the session host cannot open the repo.</exception>
    /// <exception cref="Exceptions.ExternalToolFailedException">When the review tool or the session host fails.</exception>
    Task<ReviewStarted> StartAsync(PullRequestUrl pullRequestUrl, CancellationToken cancellationToken);
}
