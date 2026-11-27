using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.GitHub;

/// <summary>GitHub's search through `gh api graphql`, so the server never holds a token.</summary>
internal sealed partial class GitHubPullRequestSearch(IProcessRunner processRunner, TimeProvider timeProvider, GitHubViewer gitHubViewer) : IPullRequestSearch
{
    private const int MaxPages = 3; // 100 pull requests per page per search
    // Pull requests asked for by id or address, and merge states, go ten at a time. GitHub works out merge state per pull
    // request on request, which makes it the costly field: in the search it made a 38-pull-request result time out (HTTP 502).
    private const int LookupBatchSize = 10;
    private const string UnknownMergeStateStatus = "UNKNOWN"; // until WithMergeStateStatusAsync looks it up
    // The open view also shows pull requests merged within this window, so a just-merged one doesn't vanish.
    private static readonly TimeSpan RecentlyMergedWindow = TimeSpan.FromHours(12);

    private const string PullRequestFields = """
        ... on PullRequest {
          id number title url isDraft state createdAt updatedAt mergedAt closedAt
          additions deletions changedFiles mergeable reviewDecision
          headRefName baseRefName
          author { login avatarUrl }
          repository { nameWithOwner name owner { login } isPrivate }
          comments(last: 100) { totalCount nodes { author { __typename login } } }
          labels(first: 10) { nodes { name color } }
          assignees(first: 10) { nodes { login } }
          reviewRequests(first: 20) {
            nodes { requestedReviewer { __typename ... on User { login } ... on Team { slug } } }
          }
          latestReviews(first: 20) { nodes { state submittedAt author { login } } }
          commits(last: 1) { nodes { commit { committedDate statusCheckRollup { state } } } }
          stack { number size }
          stackEntry { position }
        }
        """;

    private const string SearchQuery = $$"""
        query($q: String!, $after: String) {
          search(query: $q, type: ISSUE, first: 100, after: $after) {
            pageInfo { hasNextPage endCursor }
            nodes { {{PullRequestFields}} }
          }
        }
        """;

    private const string ByIdQuery = $$"""
        query($ids: [ID!]!) {
          nodes(ids: $ids) { {{PullRequestFields}} }
        }
        """;

    private const string MergeStateQuery = """
        query($ids: [ID!]!) {
          nodes(ids: $ids) { ... on PullRequest { id mergeStateStatus } }
        }
        """;

    public async Task<Inbox> BuildInboxAsync(InboxQuery query, CancellationToken cancellationToken)
    {
        var viewer = await gitHubViewer.GetAsync(cancellationToken);
        var searches = SearchScopes(query).SelectMany(RelationSearches).ToList();
        var results = await Task.WhenAll(searches.Select(async search =>
            (search.Relation, Nodes: await SearchAsync(search.Query, cancellationToken))));

        // A pull request found by several searches carries each of their relations; "involved" only when nothing else.
        var relationsById = new Dictionary<string, (PullRequestNode Node, List<PullRequestRelation> Relations)>();
        foreach (var (relation, nodes) in results)
        {
            foreach (var node in nodes)
            {
                if (node.Id is null) continue;
                if (relationsById.TryGetValue(node.Id, out var entry) == false)
                {
                    entry = (node, []);
                    relationsById[node.Id] = entry;
                }
                if (relation != PullRequestRelation.Involved) entry.Relations.Add(relation);
            }
        }

        var pullRequests = relationsById.Values
            .Select(entry => ToPullRequest(entry.Node, viewer, entry.Relations.Count > 0 ? entry.Relations : [PullRequestRelation.Involved]))
            .OfType<PullRequest>()
            .ToList();
        return new Inbox(viewer, timeProvider.GetUtcNow(), await WithMergeStateStatusAsync(pullRequests, cancellationToken));
    }

    // The inbox only reads merge state to recognise an outdated approval that GitHub still lets through (status.ts,
    // isMergeableWithOutdatedApproval). Looser than that rule on purpose: it skips the comparison of approval and commit
    // times, so the staleness rule lives in one place and a few extra lookups are the cost.
    private static bool CouldBeMergeableWithOutdatedApproval(PullRequest pullRequest) =>
        pullRequest is { State: PullRequestState.Open, IsDraft: false, ReviewDecision: "REVIEW_REQUIRED" } &&
        pullRequest.Mergeable != "CONFLICTING" &&
        pullRequest.Reviews.Any(review => review.State == "APPROVED");

    private async Task<List<PullRequest>> WithMergeStateStatusAsync(List<PullRequest> pullRequests, CancellationToken cancellationToken)
    {
        var lookupIds = pullRequests.Where(CouldBeMergeableWithOutdatedApproval).Select(pullRequest => pullRequest.Id).ToList();
        if (lookupIds.Count == 0) return pullRequests;

        var batches = await Task.WhenAll(lookupIds.Chunk(LookupBatchSize).Select(ids => MergeStateStatusesAsync(ids, cancellationToken)));
        var mergeStateStatusById = batches.SelectMany(batch => batch).ToDictionary(found => found.Id, found => found.MergeStateStatus);
        return pullRequests
            .Select(pullRequest => mergeStateStatusById.TryGetValue(pullRequest.Id, out var mergeStateStatus)
                ? pullRequest with { MergeStateStatus = mergeStateStatus }
                : pullRequest)
            .ToList();
    }

    private async Task<List<(string Id, string MergeStateStatus)>> MergeStateStatusesAsync(string[] ids, CancellationToken cancellationToken)
    {
        List<string> arguments = ["api", "graphql", "-f", $"query={MergeStateQuery}"];
        foreach (var id in ids) arguments.AddRange(["-f", $"ids[]={id}"]);
        var output = await RunGhAsync(arguments, cancellationToken);
        var nodes = JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.MergeStateEnvelope)?.Data?.Nodes
            ?? throw new ExternalToolFailedException("gh", "returned no merge state");
        return nodes
            .OfType<MergeStateNode>()
            .Where(node => node.Id is not null && node.MergeStateStatus is not null)
            .Select(node => (node.Id!, node.MergeStateStatus!))
            .ToList();
    }

    public async Task<IReadOnlyList<PullRequest>> RefreshPullRequestsAsync(IReadOnlyList<PullRequest> pullRequests, CancellationToken cancellationToken)
    {
        if (pullRequests.Count == 0) return [];
        var viewer = await gitHubViewer.GetAsync(cancellationToken);
        var relationsById = pullRequests.ToDictionary(pullRequest => pullRequest.Id, pullRequest => pullRequest.Relations);
        var batches = await Task.WhenAll(pullRequests.Select(pullRequest => pullRequest.Id).Chunk(LookupBatchSize)
            .Select(ids => PullRequestsByIdAsync(ids, cancellationToken)));
        var refreshed = batches.SelectMany(batch => batch)
            .Where(node => node.Id is not null && relationsById.ContainsKey(node.Id))
            .Select(node => ToPullRequest(node, viewer, relationsById[node.Id!]))
            .OfType<PullRequest>()
            .ToList();
        return await WithMergeStateStatusAsync(refreshed, cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequest>> FetchPullRequestsAsync(IReadOnlyList<PullRequestUrl> urls, CancellationToken cancellationToken)
    {
        if (urls.Count == 0) return [];
        var viewer = await gitHubViewer.GetAsync(cancellationToken);
        var batches = await Task.WhenAll(urls.Chunk(LookupBatchSize).Select(batch => PullRequestsByUrlAsync(batch, cancellationToken)));
        var fetched = batches.SelectMany(batch => batch)
            .Select(node => ToPullRequest(node, viewer, RelationsFrom(node, viewer)))
            .OfType<PullRequest>()
            .ToList();
        return await WithMergeStateStatusAsync(fetched, cancellationToken);
    }

    private async Task<List<PullRequestNode>> PullRequestsByIdAsync(string[] ids, CancellationToken cancellationToken)
    {
        List<string> arguments = ["api", "graphql", "-f", $"query={ByIdQuery}"];
        foreach (var id in ids) arguments.AddRange(["-f", $"ids[]={id}"]);
        var output = await RunGhAsync(arguments, cancellationToken);
        var nodes = JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.PullRequestNodesEnvelope)?.Data?.Nodes
            ?? throw new ExternalToolFailedException("gh", "returned no pull requests");
        return nodes.OfType<PullRequestNode>().ToList();
    }

    // The addresses go in as variables, one alias each, so none of their text is part of the query.
    private async Task<List<PullRequestNode>> PullRequestsByUrlAsync(PullRequestUrl[] urls, CancellationToken cancellationToken)
    {
        var variables = string.Join(", ", urls.Select((_, index) => $"$url{index}: URI!"));
        var lookups = string.Join(" ", urls.Select((_, index) => $"pr{index}: resource(url: $url{index}) {{ {PullRequestFields} }}"));
        List<string> arguments = ["api", "graphql", "-f", $"query=query({variables}) {{ {lookups} }}"];
        for (var index = 0; index < urls.Length; index++) arguments.AddRange(["-f", $"url{index}={urls[index].Value}"]);
        var output = await RunGhAsync(arguments, cancellationToken);
        var data = JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.PullRequestsByAliasEnvelope)?.Data
            ?? throw new ExternalToolFailedException("gh", "returned no pull requests");
        return data.Values.OfType<PullRequestNode>().ToList();
    }

    // What the searches would have said, as far as the pull request itself shows it. A team asked to review may be one
    // you are not in, and a mention leaves no trace on the pull request, so neither is exact.
    private static List<PullRequestRelation> RelationsFrom(PullRequestNode node, GitHubLogin viewer)
    {
        var relations = new List<PullRequestRelation>();
        var requestedReviewers = (node.ReviewRequests?.Nodes ?? []).Select(request => request?.RequestedReviewer).OfType<RequestedReviewerNode>().ToList();
        if (node.Author?.Login == viewer.Value) relations.Add(PullRequestRelation.Authored);
        if (requestedReviewers.Any(reviewer => reviewer.TypeName == "User" && reviewer.Login == viewer.Value)) relations.Add(PullRequestRelation.ReviewRequestedFromYou);
        else if (requestedReviewers.Any(reviewer => reviewer.TypeName == "Team")) relations.Add(PullRequestRelation.ReviewRequestedFromTeam);
        if (node.Author?.Login != viewer.Value && (node.LatestReviews?.Nodes ?? []).Any(review => review?.Author?.Login == viewer.Value)) relations.Add(PullRequestRelation.Reviewed);
        if ((node.Assignees?.Nodes ?? []).Any(assignee => assignee?.Login == viewer.Value)) relations.Add(PullRequestRelation.Assigned);
        return relations.Count > 0 ? relations : [PullRequestRelation.Involved];
    }

    private IEnumerable<string> SearchScopes(InboxQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var lookbackStart = now.AddDays(-query.Lookback.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return query.State switch
        {
            InboxState.Merged => [$"is:merged merged:>={lookbackStart}"],
            InboxState.Closed => [$"is:closed closed:>={lookbackStart}"],
            _ => ["is:open", $"is:merged merged:>={(now - RecentlyMergedWindow).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}"],
        };
    }

    private static IEnumerable<(PullRequestRelation Relation, string Query)> RelationSearches(string scope)
    {
        var scoped = $"is:pr archived:false {scope}";
        yield return (PullRequestRelation.Authored, $"{scoped} author:@me");
        yield return (PullRequestRelation.ReviewRequestedFromYou, $"{scoped} user-review-requested:@me");
        yield return (PullRequestRelation.ReviewRequestedFromTeam, $"{scoped} review-requested:@me -user-review-requested:@me");
        yield return (PullRequestRelation.Reviewed, $"{scoped} reviewed-by:@me -author:@me");
        yield return (PullRequestRelation.Assigned, $"{scoped} assignee:@me");
        yield return (PullRequestRelation.Mentioned, $"{scoped} mentions:@me");
        yield return (PullRequestRelation.Involved, $"{scoped} involves:@me");
    }

    private async Task<List<PullRequestNode>> SearchAsync(string searchQuery, CancellationToken cancellationToken)
    {
        var nodes = new List<PullRequestNode>();
        string? afterCursor = null;
        for (var page = 0; page < MaxPages; page++)
        {
            List<string> arguments = ["api", "graphql", "-f", $"query={SearchQuery}", "-f", $"q={searchQuery}"];
            if (afterCursor is not null) arguments.AddRange(["-f", $"after={afterCursor}"]);
            var output = await RunGhAsync(arguments, cancellationToken);
            var connection = JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.SearchEnvelope)?.Data?.Search
                ?? throw new ExternalToolFailedException("gh", "returned no search result");
            nodes.AddRange(connection.Nodes.OfType<PullRequestNode>());
            if (connection.PageInfo.HasNextPage == false) break;
            afterCursor = connection.PageInfo.EndCursor;
        }
        return nodes;
    }

    private Task<string> RunGhAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        GhCli.OutputAsync(processRunner, arguments, cancellationToken);

    /// <returns><c>null</c> for a search hit that is not a usable pull request.</returns>
    private static PullRequest? ToPullRequest(PullRequestNode node, GitHubLogin viewer, IReadOnlyList<PullRequestRelation> relations)
    {
        if (node.Id is null || node.Title is null || node.Repository is null || node.CreatedAt is null || node.UpdatedAt is null ||
            PullRequestUrl.TryParse(node.Url, out var url) == false || ToState(node.State) is not { } state)
            return null;

        var reviews = (node.LatestReviews?.Nodes ?? [])
            .OfType<ReviewNode>()
            .Select(review => new Review(review.Author?.Login ?? "ghost", review.State, ToTime(review.SubmittedAt)))
            .ToList();
        var myLatestReview = reviews.Find(review => review.Login == viewer.Value);
        var latestCommit = node.Commits?.Nodes.OfType<CommitNode>().FirstOrDefault()?.Commit;

        return new PullRequest(
            Id: node.Id,
            Url: url,
            Title: node.Title,
            IsDraft: node.IsDraft,
            State: state,
            CreatedAt: ToTime(node.CreatedAt) ?? DateTimeOffset.MinValue,
            UpdatedAt: ToTime(node.UpdatedAt) ?? DateTimeOffset.MinValue,
            MergedAt: ToTime(node.MergedAt),
            ClosedAt: ToTime(node.ClosedAt),
            Additions: node.Additions,
            Deletions: node.Deletions,
            ChangedFiles: node.ChangedFiles,
            Mergeable: node.Mergeable ?? "UNKNOWN",
            MergeStateStatus: UnknownMergeStateStatus,
            ReviewDecision: node.ReviewDecision,
            Head: node.HeadRefName ?? "",
            Base: node.BaseRefName ?? "",
            Author: node.Author?.Login ?? "ghost",
            AuthorAvatar: node.Author?.AvatarUrl,
            IsPrivate: node.Repository.IsPrivate,
            Comments: CountCommentsByAuthorKind(node.Comments),
            Labels: (node.Labels?.Nodes ?? []).OfType<LabelNode>().Select(label => new Label(label.Name, label.Color)).ToList(),
            Assignees: (node.Assignees?.Nodes ?? []).OfType<LoginNode>().Select(assignee => assignee.Login).OfType<string>().ToList(),
            ReviewRequests: ReviewRequestNames(node.ReviewRequests),
            Reviews: reviews,
            MyReview: myLatestReview?.State,
            MyReviewAt: myLatestReview?.SubmittedAt,
            LastCommitAt: ToTime(latestCommit?.CommittedDate),
            Checks: latestCommit?.StatusCheckRollup?.State,
            Stack: node.Stack is { } stack && node.StackEntry is { } entry ? new Stack(stack.Number, stack.Size, entry.Position) : null,
            Relations: relations);
    }

    private static PullRequestState? ToState(string? state) => state switch
    {
        "OPEN" => PullRequestState.Open,
        "MERGED" => PullRequestState.Merged,
        "CLOSED" => PullRequestState.Closed,
        _ => null,
    };

    private static DateTimeOffset? ToTime(string? timestamp) =>
        DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : null;

    // Only the latest 100 comments carry authors; anything older is counted as human.
    private static CommentCounts CountCommentsByAuthorKind(CommentConnection? comments)
    {
        if (comments is null) return new CommentCounts(0, 0);
        var botComments = comments.Nodes.Count(comment =>
            comment?.Author is { } author && (author.TypeName == "Bot" || BotLogin().IsMatch(author.Login ?? "")));
        return new CommentCounts(comments.TotalCount - botComments, botComments);
    }

    private static List<string> ReviewRequestNames(NodeList<ReviewRequestNode>? reviewRequests) =>
        (reviewRequests?.Nodes ?? [])
            .Select(request => request?.RequestedReviewer)
            .OfType<RequestedReviewerNode>()
            .Select(reviewer => reviewer.TypeName == "Team" ? $"team:{reviewer.Slug}" : reviewer.Login)
            .OfType<string>()
            .ToList();

    [GeneratedRegex(@"\[bot\]\z")]
    private static partial Regex BotLogin();
}
