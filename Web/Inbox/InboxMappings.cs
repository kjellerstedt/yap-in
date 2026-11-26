using System.Globalization;
using YapIn.Api.Responses;
using YapIn.Domain.Models;

namespace YapIn.Web.Inbox;

internal static class InboxMappings
{
    public static InboxResponse ToResponse(this Domain.Models.Inbox inbox) => new(
        inbox.Viewer.Value,
        inbox.FetchedAt.ToString("O", CultureInfo.InvariantCulture),
        inbox.PullRequests.Select(ToResponse).ToList());

    private static PullRequestResponse ToResponse(PullRequest pullRequest) => new(
        Id: pullRequest.Id,
        Number: pullRequest.Url.Number.Value,
        Title: pullRequest.Title,
        Url: pullRequest.Url.Value,
        IsDraft: pullRequest.IsDraft,
        State: pullRequest.State switch
        {
            PullRequestState.Merged => "MERGED",
            PullRequestState.Closed => "CLOSED",
            _ => "OPEN",
        },
        CreatedAt: GitHubTime(pullRequest.CreatedAt),
        UpdatedAt: GitHubTime(pullRequest.UpdatedAt),
        MergedAt: GitHubTime(pullRequest.MergedAt),
        ClosedAt: GitHubTime(pullRequest.ClosedAt),
        Additions: pullRequest.Additions,
        Deletions: pullRequest.Deletions,
        ChangedFiles: pullRequest.ChangedFiles,
        Mergeable: pullRequest.Mergeable,
        MergeStateStatus: pullRequest.MergeStateStatus,
        ReviewDecision: pullRequest.ReviewDecision,
        Head: pullRequest.Head,
        Base: pullRequest.Base,
        Author: pullRequest.Author,
        AuthorAvatar: pullRequest.AuthorAvatar,
        Repo: $"{pullRequest.Url.Owner}/{pullRequest.Url.Repository}",
        Owner: pullRequest.Url.Owner.Value,
        IsPrivate: pullRequest.IsPrivate,
        Comments: new CommentCountsResponse(pullRequest.Comments.Human, pullRequest.Comments.Bot),
        Labels: pullRequest.Labels.Select(label => new LabelResponse(label.Name, label.Color)).ToList(),
        Assignees: pullRequest.Assignees,
        ReviewRequests: pullRequest.ReviewRequests,
        Reviews: pullRequest.Reviews.Select(review => new ReviewResponse(review.Login, review.State, GitHubTime(review.SubmittedAt))).ToList(),
        MyReview: pullRequest.MyReview,
        MyReviewAt: GitHubTime(pullRequest.MyReviewAt),
        LastCommitAt: GitHubTime(pullRequest.LastCommitAt),
        Checks: pullRequest.Checks,
        Stack: pullRequest.Stack is { } stack ? new StackResponse(stack.Number, stack.Size, stack.Position) : null,
        Relations: pullRequest.Relations.Select(RelationName).ToList());

    // The page stores these and compares them as text with later ones, so they keep GitHub's exact format.
    private static string GitHubTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? GitHubTime(DateTimeOffset? time) => time is { } value ? GitHubTime(value) : null;

    private static string RelationName(PullRequestRelation relation) => relation switch
    {
        PullRequestRelation.Authored => "authored",
        PullRequestRelation.ReviewRequestedFromYou => "review-you",
        PullRequestRelation.ReviewRequestedFromTeam => "review-team",
        PullRequestRelation.Reviewed => "reviewed",
        PullRequestRelation.Assigned => "assigned",
        PullRequestRelation.Mentioned => "mentioned",
        _ => "involved",
    };
}
