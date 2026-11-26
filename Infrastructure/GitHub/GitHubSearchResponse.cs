using System.Text.Json.Serialization;

namespace YapIn.Infrastructure.GitHub;

// The GraphQL search response as GitHub sends it. These types stay inside Infrastructure: the domain only ever sees
// PullRequest, so a change on GitHub's side is absorbed in one place (GitHubPullRequestSearch.ToPullRequest).

internal sealed record SearchEnvelope(SearchData? Data);

internal sealed record SearchData(SearchConnection Search);

internal sealed record SearchConnection(PageInfo PageInfo, IReadOnlyList<PullRequestNode?> Nodes);

internal sealed record PageInfo(bool HasNextPage, string? EndCursor);

// Search results that are not pull requests arrive as empty objects, so every field may be missing.
internal sealed record PullRequestNode(
    string? Id,
    int Number,
    string? Title,
    string? Url,
    bool IsDraft,
    string? State,
    string? CreatedAt,
    string? UpdatedAt,
    string? MergedAt,
    string? ClosedAt,
    int Additions,
    int Deletions,
    int ChangedFiles,
    string? Mergeable,
    string? ReviewDecision,
    string? HeadRefName,
    string? BaseRefName,
    ActorNode? Author,
    RepositoryNode? Repository,
    CommentConnection? Comments,
    NodeList<LabelNode>? Labels,
    NodeList<LoginNode>? Assignees,
    NodeList<ReviewRequestNode>? ReviewRequests,
    NodeList<ReviewNode>? LatestReviews,
    NodeList<CommitNode>? Commits,
    StackNode? Stack,
    StackEntryNode? StackEntry);

internal sealed record NodeList<T>(IReadOnlyList<T?> Nodes);

internal sealed record ActorNode(string? Login, string? AvatarUrl);

internal sealed record LoginNode(string? Login);

internal sealed record RepositoryNode(string NameWithOwner, string Name, LoginNode Owner, bool IsPrivate);

internal sealed record CommentConnection(int TotalCount, IReadOnlyList<CommentNode?> Nodes);

internal sealed record CommentNode(TypedActorNode? Author);

internal sealed record TypedActorNode([property: JsonPropertyName("__typename")] string? TypeName, string? Login);

internal sealed record LabelNode(string Name, string Color);

internal sealed record ReviewRequestNode(RequestedReviewerNode? RequestedReviewer);

internal sealed record RequestedReviewerNode([property: JsonPropertyName("__typename")] string? TypeName, string? Login, string? Slug);

internal sealed record ReviewNode(string State, string? SubmittedAt, LoginNode? Author);

internal sealed record CommitNode(CommitDetails Commit);

internal sealed record CommitDetails(string? CommittedDate, StatusCheckRollupNode? StatusCheckRollup);

internal sealed record StatusCheckRollupNode(string? State);

internal sealed record StackNode(int Number, int Size);

internal sealed record StackEntryNode(int Position);
