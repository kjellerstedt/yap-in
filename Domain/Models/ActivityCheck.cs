using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Models;

/// <summary>What GitHub's activity feeds said since the last check, and how long to wait before asking again.</summary>
/// <param name="HasChanged">Something happened: an action of yours or a notification. Always false on the first check, which only sets the baseline.</param>
/// <param name="NotifiedPullRequests">Pull requests with a notification that was not there at the last check.</param>
public sealed record ActivityCheck(bool HasChanged, IReadOnlyList<PullRequestUrl> NotifiedPullRequests, TimeSpan PollInterval);
