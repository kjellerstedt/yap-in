namespace YapIn.Api.Responses;

// The inbox as the page receives it. Field names and value formats match what the page has stored since before the
// rewrite: timestamps are GitHub's own format (2026-11-20T09:12:39Z), because the page compares them as strings against
// saved values. frontend/src/inbox/types.ts mirrors these records; PullRequestResponseShapeTests fails when the two drift.

public sealed record InboxResponse(string Viewer, string FetchedAt, IReadOnlyList<PullRequestResponse> Prs);

public sealed record PullRequestResponse(
    string Id,
    int Number,
    string Title,
    string Url,
    bool IsDraft,
    string State,
    string CreatedAt,
    string UpdatedAt,
    string? MergedAt,
    string? ClosedAt,
    int Additions,
    int Deletions,
    int ChangedFiles,
    string Mergeable,
    string MergeStateStatus,
    string? ReviewDecision,
    string Head,
    string Base,
    string Author,
    string? AuthorAvatar,
    string Repo,
    string Owner,
    bool IsPrivate,
    CommentCountsResponse Comments,
    IReadOnlyList<LabelResponse> Labels,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> ReviewRequests,
    IReadOnlyList<ReviewResponse> Reviews,
    string? MyReview,
    string? MyReviewAt,
    string? LastCommitAt,
    string? Checks,
    StackResponse? Stack,
    // "authored", "review-you", "review-team", "reviewed", "assigned", "mentioned" or "involved".
    IReadOnlyList<string> Relations);

public sealed record CommentCountsResponse(int Human, int Bot);

public sealed record LabelResponse(string Name, string Color);

public sealed record ReviewResponse(string Login, string State, string? SubmittedAt);

public sealed record StackResponse(int Number, int Size, int Position);
