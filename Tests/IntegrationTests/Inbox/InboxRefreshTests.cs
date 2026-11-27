using YapIn.Api.Responses;
using YapIn.IntegrationTests.Fakes;
using static YapIn.IntegrationTests.Assertions;

namespace YapIn.IntegrationTests.Inbox;

/// <summary>The background refresh, one pass at a time: a pass is what the loop runs after each wait.</summary>
public sealed class InboxRefreshTests
{
    private static readonly TimeSpan OnePoll = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Fetches_the_open_pull_requests_again_when_your_activity_changed_and_does_not_search()
    {
        // Arrange
        await using var fixture = new Fixture();
        var eventsEntityTag = "events-1";
        fixture.Programs.Respond = GitHubResponses.Answering(
            AuthoredBy(GitHubResponses.PullRequest("A", 1, author: GitHubResponses.Viewer)),
            pullRequestsById: _ => [GitHubResponses.PullRequest("A", 1, author: GitHubResponses.Viewer, reviewDecision: "APPROVED")],
            activity: (path, ifNoneMatch) => path.Contains("/events", StringComparison.Ordinal)
                ? GitHubResponses.Feed(eventsEntityTag, ifNoneMatch)
                : GitHubResponses.Feed("notifications-1", ifNoneMatch));
        await fixture.Driver.ListInboxAsync(state: "open");
        var searchesBefore = SearchQueries(fixture).Count;
        var refreshService = fixture.CreateRefreshService();

        // Act
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);
        var idLookupsAfterBaseline = FetchedByIds(fixture).Count;
        eventsEntityTag = "events-2";
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, idLookupsAfterBaseline);
        Assert.Equal(["A"], FetchedByIds(fixture));
        Assert.Equal(searchesBefore, SearchQueries(fixture).Count);
        var pullRequest = Assert.Single(AssertOk<InboxResponse>(await fixture.Driver.ListInboxAsync(state: "open")).Prs);
        Assert.Equal("APPROVED", pullRequest.ReviewDecision);
        Assert.Equal(["authored"], pullRequest.Relations);
    }

    [Fact]
    public async Task Asks_nothing_more_when_both_feeds_say_nothing_changed()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(
            AuthoredBy(GitHubResponses.PullRequest("A", 1)),
            activity: (path, ifNoneMatch) => GitHubResponses.Feed(path.Contains("/events", StringComparison.Ordinal) ? "events-1" : "notifications-1", ifNoneMatch));
        await fixture.Driver.ListInboxAsync(state: "open");
        var refreshService = fixture.CreateRefreshService();
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken); // the baseline
        var requestsAfterBaseline = fixture.Programs.Requests.Count;

        // Act
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, fixture.Programs.Requests.Count - requestsAfterBaseline); // the two feeds, both answered 304
        Assert.Empty(FetchedByIds(fixture));
    }

    [Fact]
    public async Task Adds_an_open_notified_pull_request_the_inbox_does_not_have_and_works_out_how_it_involves_you()
    {
        // Arrange
        await using var fixture = new Fixture();
        const string cachedUrl = "https://github.com/acme/search/pull/1";
        const string newUrl = "https://github.com/acme/search/pull/7";
        const string mergedUrl = "https://github.com/acme/search/pull/8";
        var notifications = "[]";
        var notificationsEntityTag = "notifications-1";
        fixture.Programs.Respond = GitHubResponses.Answering(
            AuthoredBy(GitHubResponses.PullRequest("A", 1)),
            pullRequestsById: _ => [GitHubResponses.PullRequest("A", 1)],
            pullRequestsByUrl: urls => urls.Select(url => url == newUrl
                ? GitHubResponses.PullRequest("NEW", 7, author: GitHubResponses.Viewer)
                : GitHubResponses.PullRequest("MERGED", 8, state: "MERGED")).ToArray(),
            activity: (path, ifNoneMatch) => path.Contains("/events", StringComparison.Ordinal)
                ? GitHubResponses.Feed("events-1", ifNoneMatch)
                : GitHubResponses.Feed(notificationsEntityTag, ifNoneMatch, notifications));
        await fixture.Driver.ListInboxAsync(state: "open");
        var refreshService = fixture.CreateRefreshService();
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken); // the baseline

        // Act
        notificationsEntityTag = "notifications-2";
        notifications = $"[{GitHubResponses.Notification("t1", "2026-11-20T12:01:00Z", cachedUrl)},{GitHubResponses.Notification("t2", "2026-11-20T12:01:30Z", newUrl)},{GitHubResponses.Notification("t3", "2026-11-20T12:01:40Z", mergedUrl)}]";
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([newUrl, mergedUrl], fixture.Programs.RequestsTo("gh").SelectMany(GitHubResponses.UrlsAskedFor)); // not the one it already has
        var pullRequests = AssertOk<InboxResponse>(await fixture.Driver.ListInboxAsync(state: "open")).Prs.ToDictionary(pullRequest => pullRequest.Id);
        Assert.Equal(["A", "NEW"], pullRequests.Keys.Order()); // the merged one is not an open pull request
        Assert.Equal(["authored", "review-team"], pullRequests["NEW"].Relations);
    }

    [Fact]
    public async Task Does_not_fetch_a_notified_pull_request_again_for_a_notification_it_already_saw()
    {
        // Arrange
        await using var fixture = new Fixture();
        const string mergedUrl = "https://github.com/acme/search/pull/8";
        var notificationsEntityTag = "notifications-1";
        var notifications = "[]";
        fixture.Programs.Respond = GitHubResponses.Answering(
            AuthoredBy(GitHubResponses.PullRequest("A", 1)),
            pullRequestsByUrl: _ => [GitHubResponses.PullRequest("MERGED", 8, state: "MERGED")],
            activity: (path, ifNoneMatch) => path.Contains("/events", StringComparison.Ordinal)
                ? GitHubResponses.Feed("events-1", ifNoneMatch)
                : GitHubResponses.Feed(notificationsEntityTag, ifNoneMatch, notifications));
        await fixture.Driver.ListInboxAsync(state: "open");
        var refreshService = fixture.CreateRefreshService();
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken); // the baseline
        var aboutMerged = GitHubResponses.Notification("t3", "2026-11-20T12:01:40Z", mergedUrl);

        // Act
        notificationsEntityTag = "notifications-2";
        notifications = $"[{aboutMerged}]";
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);
        notificationsEntityTag = "notifications-3"; // another thread is read, so the feed changes but this one is the same
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(fixture.Programs.RequestsTo("gh").SelectMany(GitHubResponses.UrlsAskedFor));
    }

    [Fact]
    public async Task Rebuilds_every_inbox_with_the_full_search_every_five_minutes_and_not_before()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(_ => []);
        await fixture.Driver.ListInboxAsync(state: "open");
        var searchesAfterFirstBuild = SearchQueries(fixture).Count;
        var refreshService = fixture.CreateRefreshService();

        // Act
        for (var minute = 1; minute <= 4; minute++)
        {
            fixture.Clock.Advance(OnePoll);
            await refreshService.RunPassAsync(TestContext.Current.CancellationToken);
        }
        var searchesAfterFourMinutes = SearchQueries(fixture).Count;
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(searchesAfterFirstBuild, searchesAfterFourMinutes);
        Assert.Equal(2 * searchesAfterFirstBuild, SearchQueries(fixture).Count);
    }

    [Fact]
    public async Task Keeps_the_cached_inbox_and_carries_on_when_asking_GitHub_for_activity_fails()
    {
        // Arrange
        await using var fixture = new Fixture();
        var answer = GitHubResponses.Answering(AuthoredBy(GitHubResponses.PullRequest("A", 1)));
        fixture.Programs.Respond = request => request.Arguments.Contains("--include") ? FakeProcessRunnerFails() : answer(request);
        await fixture.Driver.ListInboxAsync(state: "open");
        var refreshService = fixture.CreateRefreshService();

        // Act
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(AssertOk<InboxResponse>(await fixture.Driver.ListInboxAsync(state: "open")).Prs);
    }

    [Fact]
    public async Task Waits_as_long_as_GitHub_asks_between_checks_and_never_less_than_a_minute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var pollInterval = 120;
        fixture.Programs.Respond = GitHubResponses.Answering(_ => [], activity: (_, _) => GitHubResponses.FeedChanged("tag", pollInterval: pollInterval));
        await fixture.Driver.ListInboxAsync(state: "open");
        var refreshService = fixture.CreateRefreshService();

        // Act
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);
        var intervalGitHubAskedFor = refreshService.PollInterval;
        pollInterval = 10;
        fixture.Clock.Advance(OnePoll);
        await refreshService.RunPassAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(120), intervalGitHubAskedFor);
        Assert.Equal(OnePoll, refreshService.PollInterval);
    }

    private static Func<string, string[]> AuthoredBy(params string[] pullRequests) => searchQuery =>
        searchQuery.Contains("is:open", StringComparison.Ordinal) && searchQuery.EndsWith(" author:@me", StringComparison.Ordinal) ? pullRequests : [];

    private static YapIn.Infrastructure.Processes.ProcessResult FakeProcessRunnerFails() => YapIn.Tests.Shared.FakeProcessRunner.Fails("gh: HTTP 403");

    private static List<string> SearchQueries(Fixture fixture) =>
        fixture.Programs.RequestsTo("gh").SelectMany(request => request.Arguments)
            .Where(argument => argument.StartsWith("q=", StringComparison.Ordinal)).ToList();

    /// <summary>The ids asked for by a fetch of pull requests; a merge state lookup is not one.</summary>
    private static List<string> FetchedByIds(Fixture fixture) =>
        fixture.Programs.RequestsTo("gh")
            .Where(request => request.Arguments.Any(argument => argument.StartsWith("query=", StringComparison.Ordinal) &&
                argument.Contains("mergeStateStatus", StringComparison.Ordinal) == false))
            .SelectMany(GitHubResponses.IdsAskedFor)
            .ToList();
}
