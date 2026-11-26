using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Models;

/// <summary>The pull requests that involve you, as of <see cref="FetchedAt"/>.</summary>
public sealed record Inbox(GitHubLogin Viewer, DateTimeOffset FetchedAt, IReadOnlyList<PullRequest> PullRequests);

/// <summary>Which inbox to build: the state and, for merged and closed, how far back.</summary>
public sealed record InboxQuery(InboxState State, LookbackDays Lookback);

public enum PullRequestState
{
    Open,
    Merged,
    Closed,
}

/// <summary>How you are connected to a pull request; each comes from its own search.</summary>
public enum PullRequestRelation
{
    Authored,
    ReviewRequestedFromYou,
    ReviewRequestedFromTeam,
    Reviewed,
    Assigned,
    Mentioned,
    Involved,
}

// GitHub's own enum values (mergeable, review decision, review state, check rollup) pass through as text: the inbox
// only compares them, and GitHub adds values over time.
public sealed record PullRequest(
    string Id,
    PullRequestUrl Url,
    string Title,
    bool IsDraft,
    PullRequestState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MergedAt,
    DateTimeOffset? ClosedAt,
    int Additions,
    int Deletions,
    int ChangedFiles,
    string Mergeable,
    string? ReviewDecision,
    string Head,
    string Base,
    string Author,
    string? AuthorAvatar,
    bool IsPrivate,
    CommentCounts Comments,
    IReadOnlyList<Label> Labels,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> ReviewRequests,
    IReadOnlyList<Review> Reviews,
    string? MyReview,
    DateTimeOffset? MyReviewAt,
    DateTimeOffset? LastCommitAt,
    string? Checks,
    Stack? Stack,
    IReadOnlyList<PullRequestRelation> Relations);

public sealed record CommentCounts(int Human, int Bot);

public sealed record Label(string Name, string Color);

public sealed record Review(string Login, string State, DateTimeOffset? SubmittedAt);

public sealed record Stack(int Number, int Size, int Position);
