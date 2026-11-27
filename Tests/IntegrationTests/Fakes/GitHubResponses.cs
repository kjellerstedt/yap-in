using System.Globalization;
using System.Text.RegularExpressions;
using YapIn.Infrastructure.Processes;
using YapIn.Tests.Shared;

namespace YapIn.IntegrationTests.Fakes;

/// <summary>gh's output, shaped as GitHub's GraphQL API sends it, for <see cref="FakeProcessRunner.Respond"/>.</summary>
internal static partial class GitHubResponses
{
    public const string Viewer = "kjellerstedt";

    public const string IdsArgumentPrefix = "ids[]=";

    /// <summary>
    /// Answers the login lookup with <see cref="Viewer"/>, each search with the nodes <paramref name="nodesFor"/>
    /// returns for its query (the text after <c>q=</c>), and each merge state lookup with <c>CLEAN</c> for every id asked for.
    /// A fetch by id gets the nodes <paramref name="pullRequestsById"/> returns for the ids, and a fetch by address the nodes
    /// <paramref name="pullRequestsByUrl"/> returns for the addresses (<c>"null"</c> for one GitHub cannot find). An activity feed
    /// is asked through <paramref name="activity"/> with its path and the ETag sent; by default nothing has changed.
    /// </summary>
    public static Func<ProcessRequest, ProcessResult> Answering(
        Func<string, string[]> nodesFor,
        Func<IReadOnlyList<string>, string[]>? pullRequestsById = null,
        Func<IReadOnlyList<string>, string[]>? pullRequestsByUrl = null,
        Func<string, string?, ProcessResult>? activity = null) => request =>
    {
        var arguments = request.Arguments;
        if (arguments.Contains("--include"))
        {
            var ifNoneMatch = arguments.FirstOrDefault(argument => argument.StartsWith("If-None-Match: ", StringComparison.Ordinal))?["If-None-Match: ".Length..];
            return (activity ?? ((_, _) => FeedNotModified()))(arguments[arguments.ToList().IndexOf("--include") + 1], ifNoneMatch);
        }
        if (arguments.Contains("user")) return FakeProcessRunner.Succeeds($"{Viewer}\n");
        if (IdsAskedFor(request).Count > 0)
        {
            var isMergeStateLookup = arguments.Any(argument => argument.StartsWith("query=", StringComparison.Ordinal) && argument.Contains("mergeStateStatus", StringComparison.Ordinal));
            return FakeProcessRunner.Succeeds(isMergeStateLookup
                ? MergeStatePage(IdsAskedFor(request))
                : """{"data":{"nodes":[NODES]}}""".Replace("NODES", string.Join(",", (pullRequestsById ?? (_ => []))(IdsAskedFor(request))), StringComparison.Ordinal));
        }
        var urls = UrlsAskedFor(request);
        if (urls.Count > 0)
        {
            var nodes = (pullRequestsByUrl ?? (_ => []))(urls);
            return FakeProcessRunner.Succeeds("""{"data":{ALIASES}}""".Replace("ALIASES",
                string.Join(",", nodes.Select((node, index) => $"\"pr{index}\":{node}")), StringComparison.Ordinal));
        }
        var searchQuery = arguments.Single(argument => argument.StartsWith("q=", StringComparison.Ordinal))[2..];
        return FakeProcessRunner.Succeeds(SearchPage(nodesFor(searchQuery)));
    };

    /// <summary>What gh prints with --include for a feed that has a new answer.</summary>
    public static ProcessResult FeedChanged(string entityTag, string body = "[]", int pollInterval = 60) =>
        FakeProcessRunner.Succeeds($"HTTP/2.0 200 OK\r\nEtag: {entityTag}\r\nX-Poll-Interval: {pollInterval}\r\n\r\n{body}");

    /// <summary>What gh prints, and exits 1 with, when GitHub answers 304.</summary>
    public static ProcessResult FeedNotModified() => new(1, "HTTP/2.0 304 Not Modified\r\nX-Poll-Interval: 60\r\n\r\n", "");

    /// <summary>A feed that answers with <paramref name="currentEntityTag"/> and 304 to anyone who already has it, as GitHub does.</summary>
    public static ProcessResult Feed(string currentEntityTag, string? ifNoneMatch, string body = "[]", int pollInterval = 60) =>
        ifNoneMatch == currentEntityTag ? FeedNotModified() : FeedChanged(currentEntityTag, body, pollInterval);

    public static string Notification(string threadId, string updatedAt, string pullRequestUrl)
    {
        var apiUrl = pullRequestUrl.Replace("https://github.com/", "https://api.github.com/repos/", StringComparison.Ordinal)
            .Replace("/pull/", "/pulls/", StringComparison.Ordinal);
        return $$$"""{"id":"{{{threadId}}}","updated_at":"{{{updatedAt}}}","subject":{"type":"PullRequest","url":"{{{apiUrl}}}"}}""";
    }

    /// <summary>The addresses of a fetch by address, which gh is given one <c>urlN=</c> argument per pull request.</summary>
    public static List<string> UrlsAskedFor(ProcessRequest request) =>
        request.Arguments.Where(argument => UrlArgument().IsMatch(argument)).Select(argument => argument[(argument.IndexOf('=', StringComparison.Ordinal) + 1)..]).ToList();

    [GeneratedRegex(@"\Aurl[0-9]+=")]
    private static partial Regex UrlArgument();

    /// <summary>The ids of a merge state lookup, which gh is given one <c>ids[]=</c> argument per pull request.</summary>
    public static List<string> IdsAskedFor(ProcessRequest request) =>
        request.Arguments.Where(argument => argument.StartsWith(IdsArgumentPrefix, StringComparison.Ordinal))
            .Select(argument => argument[IdsArgumentPrefix.Length..])
            .ToList();

    private static string MergeStatePage(IEnumerable<string> ids) =>
        """{"data":{"nodes":[NODES]}}""".Replace("NODES",
            string.Join(",", ids.Select(id => $$"""{"id":"{{id}}","mergeStateStatus":"CLEAN"}""")), StringComparison.Ordinal);

    public static string SearchPage(params string[] nodes) =>
        """{"data":{"search":{"pageInfo":{"hasNextPage":false,"endCursor":null},"nodes":[NODES]}}}"""
            .Replace("NODES", string.Join(",", nodes), StringComparison.Ordinal);

    /// <summary>A search hit that is not a pull request, which GitHub sends as an empty object.</summary>
    public const string NotAPullRequest = "{}";

    public static string PullRequest(
        string id, int number, string author = "anna", string reviews = "[]", string reviewDecision = "REVIEW_REQUIRED",
        string mergeable = "MERGEABLE", bool isDraft = false, string state = "OPEN") => """
        {"id":"__ID__","number":__NUMBER__,"title":"Title __NUMBER__","url":"https://github.com/acme/search/pull/__NUMBER__",
         "isDraft":__DRAFT__,"state":"__STATE__","createdAt":"2026-11-19T08:00:00Z","updatedAt":"2026-11-20T09:00:00Z",
         "mergedAt":null,"closedAt":null,"additions":10,"deletions":2,"changedFiles":1,"mergeable":"__MERGEABLE__",
         "reviewDecision":"__DECISION__","headRefName":"feat/x","baseRefName":"main",
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
        .Replace("__REVIEWS__", reviews, StringComparison.Ordinal)
        .Replace("__DECISION__", reviewDecision, StringComparison.Ordinal)
        .Replace("__MERGEABLE__", mergeable, StringComparison.Ordinal)
        .Replace("__DRAFT__", isDraft ? "true" : "false", StringComparison.Ordinal)
        .Replace("__STATE__", state, StringComparison.Ordinal);

    public static string ApprovedBy(string login, string submittedAt) =>
        $$$"""[{"state":"APPROVED","submittedAt":"{{{submittedAt}}}","author":{"login":"{{{login}}}"}}]""";
}
