using System.Globalization;
using YapIn.Infrastructure.Processes;
using YapIn.Tests.Shared;

namespace YapIn.IntegrationTests.Fakes;

/// <summary>gh's output, shaped as GitHub's GraphQL API sends it, for <see cref="FakeProcessRunner.Respond"/>.</summary>
internal static class GitHubResponses
{
    public const string Viewer = "kjellerstedt";

    /// <summary>
    /// Answers the login lookup with <see cref="Viewer"/> and each search with the nodes <paramref name="nodesFor"/>
    /// returns for its query (the text after <c>q=</c>).
    /// </summary>
    public static Func<ProcessRequest, ProcessResult> Answering(Func<string, string[]> nodesFor) => request =>
    {
        if (request.Arguments.Contains("user")) return FakeProcessRunner.Succeeds($"{Viewer}\n");
        var searchQuery = request.Arguments.Single(argument => argument.StartsWith("q=", StringComparison.Ordinal))[2..];
        return FakeProcessRunner.Succeeds(SearchPage(nodesFor(searchQuery)));
    };

    public static string SearchPage(params string[] nodes) =>
        """{"data":{"search":{"pageInfo":{"hasNextPage":false,"endCursor":null},"nodes":[NODES]}}}"""
            .Replace("NODES", string.Join(",", nodes), StringComparison.Ordinal);

    /// <summary>A search hit that is not a pull request, which GitHub sends as an empty object.</summary>
    public const string NotAPullRequest = "{}";

    public static string PullRequest(string id, int number, string author = "anna", string reviews = "[]") => """
        {"id":"__ID__","number":__NUMBER__,"title":"Title __NUMBER__","url":"https://github.com/acme/search/pull/__NUMBER__",
         "isDraft":false,"state":"OPEN","createdAt":"2026-11-19T08:00:00Z","updatedAt":"2026-11-20T09:00:00Z",
         "mergedAt":null,"closedAt":null,"additions":10,"deletions":2,"changedFiles":1,"mergeable":"MERGEABLE",
         "reviewDecision":"REVIEW_REQUIRED","headRefName":"feat/x","baseRefName":"main",
         "author":{"login":"__AUTHOR__","avatarUrl":"https://avatars.example/u"},
         "repository":{"nameWithOwner":"acme/search","name":"search","owner":{"login":"acme"},"isPrivate":true},
         "comments":{"totalCount":3,"nodes":[{"author":{"__typename":"Bot","login":"ci"}},{"author":{"__typename":"User","login":"a"}}]},
         "labels":{"nodes":[{"name":"bug","color":"ff0000"}]},"assignees":{"nodes":[]},
         "reviewRequests":{"nodes":[{"requestedReviewer":{"__typename":"Team","slug":"cx"}}]},
         "latestReviews":{"nodes":__REVIEWS__},
         "commits":{"nodes":[{"commit":{"committedDate":"2026-11-20T09:12:39Z","statusCheckRollup":{"state":"SUCCESS"}}}]},
         "stack":null,"stackEntry":null}
        """
        .Replace("__ID__", id, StringComparison.Ordinal)
        .Replace("__NUMBER__", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("__AUTHOR__", author, StringComparison.Ordinal)
        .Replace("__REVIEWS__", reviews, StringComparison.Ordinal);

    public static string ApprovedBy(string login, string submittedAt) =>
        $$$"""[{"state":"APPROVED","submittedAt":"{{{submittedAt}}}","author":{"login":"{{{login}}}"}}]""";
}
