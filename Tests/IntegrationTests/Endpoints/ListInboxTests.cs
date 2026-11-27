using YapIn.Api.Responses;
using YapIn.IntegrationTests.Fakes;
using YapIn.Tests.Shared;
using static YapIn.IntegrationTests.Assertions;

namespace YapIn.IntegrationTests.Endpoints;

public sealed class ListInboxTests
{
    [Fact]
    public async Task Combines_each_relation_search_into_one_inbox()
    {
        // Arrange
        await using var fixture = new Fixture();
        var myApproval = GitHubResponses.ApprovedBy(GitHubResponses.Viewer, "2026-11-20T08:01:49Z");
        fixture.Programs.Respond = GitHubResponses.Answering(searchQuery => searchQuery switch
        {
            _ when searchQuery.Contains("is:open", StringComparison.Ordinal) == false => [GitHubResponses.NotAPullRequest],
            _ when searchQuery.EndsWith(" author:@me", StringComparison.Ordinal) => [GitHubResponses.PullRequest("A", 1)],
            _ when searchQuery.EndsWith(" involves:@me", StringComparison.Ordinal) =>
                [GitHubResponses.PullRequest("A", 1), GitHubResponses.PullRequest("B", 2, reviews: myApproval), GitHubResponses.NotAPullRequest],
            _ => [],
        });

        // Act
        var result = await fixture.Driver.ListInboxAsync(state: "open");

        // Assert
        var inbox = AssertOk<InboxResponse>(result);
        Assert.Equal(GitHubResponses.Viewer, inbox.Viewer);
        var prs = inbox.Prs.ToDictionary(pr => pr.Id);
        Assert.Equal(["A", "B"], prs.Keys.Order());
        Assert.Equal(["authored"], prs["A"].Relations);
        Assert.Equal(["involved"], prs["B"].Relations);

        var approved = prs["B"];
        Assert.Equal("APPROVED", approved.MyReview);
        Assert.Equal("2026-11-20T08:01:49Z", approved.MyReviewAt);
        // Every reviewer's latest review reaches the page, which compares their times to tell an approval after a request for changes.
        Assert.Equal([new ReviewResponse(GitHubResponses.Viewer, "APPROVED", "2026-11-20T08:01:49Z")], approved.Reviews);
        Assert.Equal("2026-11-20T09:12:39Z", approved.LastCommitAt);
        Assert.Equal("2026-11-20T09:00:00Z", approved.UpdatedAt);
        Assert.Equal("acme/search", approved.Repo);
        Assert.Equal("acme", approved.Owner);
        Assert.Equal(new CommentCountsResponse(Human: 2, Bot: 1), approved.Comments);
        Assert.Equal(["team:cx"], approved.ReviewRequests);
        Assert.Equal("SUCCESS", approved.Checks);
        Assert.Null(prs["A"].MyReview);
    }

    [Fact]
    public async Task Open_view_also_searches_pull_requests_merged_in_the_last_12_hours()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(_ => []);

        // Act
        await fixture.Driver.ListInboxAsync(state: "open");

        // Assert
        var searchQueries = SearchQueries(fixture);
        Assert.Equal(14, searchQueries.Count);
        Assert.Equal(7, searchQueries.Count(query => query.Contains(" is:open ", StringComparison.Ordinal)));
        Assert.Equal(7, searchQueries.Count(query => query.Contains(" is:merged merged:>=2026-11-20T00:00:00Z ", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("merged", "30", "is:merged merged:>=2026-10-21")]
    [InlineData("closed", "7", "is:closed closed:>=2026-11-13")]
    public async Task Merged_and_closed_views_search_back_the_chosen_number_of_days(string state, string days, string expectedScope)
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(_ => []);

        // Act
        await fixture.Driver.ListInboxAsync(state, days);

        // Assert
        Assert.All(SearchQueries(fixture), query => Assert.Contains($" {expectedScope} ", query, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("anything", "99999")]
    [InlineData(null, "0")]
    [InlineData("OPEN", "-1")]
    public async Task Falls_back_to_the_open_view_for_values_it_does_not_know(string? state, string? days)
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(_ => []);

        // Act
        var result = await fixture.Driver.ListInboxAsync(state, days);

        // Assert
        AssertOk<InboxResponse>(result);
        Assert.Contains(SearchQueries(fixture), query => query.Contains(" is:open ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Serves_the_cached_inbox_until_asked_to_rebuild()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(_ => []);
        await fixture.Driver.ListInboxAsync();
        var searchesAfterFirstLoad = SearchQueries(fixture).Count;

        // Act
        await fixture.Driver.ListInboxAsync();
        var searchesAfterCachedLoad = SearchQueries(fixture).Count;
        await fixture.Driver.ListInboxAsync(rebuild: true);

        // Assert
        Assert.Equal(searchesAfterFirstLoad, searchesAfterCachedLoad);
        Assert.Equal(2 * searchesAfterFirstLoad, SearchQueries(fixture).Count);
    }

    [Fact]
    public async Task Tells_the_page_a_fixed_message_when_gh_fails_and_keeps_gh_output_out_of_it()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = _ => FakeProcessRunner.Fails("HTTP 401: token ghp_secret is invalid");

        // Act
        var result = await fixture.Driver.ListInboxAsync();

        // Assert
        var error = AssertError(result, 502);
        Assert.DoesNotContain("ghp_secret", error, StringComparison.Ordinal);
        Assert.Contains("Is gh signed in?", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Looks_up_merge_state_only_for_open_pull_requests_that_could_be_mergeable_with_an_outdated_approval()
    {
        // Arrange
        await using var fixture = new Fixture();
        var approval = GitHubResponses.ApprovedBy("pelto", "2026-11-20T08:01:49Z");
        fixture.Programs.Respond = GitHubResponses.Answering(searchQuery => searchQuery switch
        {
            _ when searchQuery.Contains("is:open", StringComparison.Ordinal) == false => [],
            _ when searchQuery.EndsWith(" author:@me", StringComparison.Ordinal) =>
            [
                GitHubResponses.PullRequest("CANDIDATE", 1, reviews: approval),
                GitHubResponses.PullRequest("NOT_REVIEWED", 2),
                GitHubResponses.PullRequest("ALREADY_APPROVED", 3, reviews: approval, reviewDecision: "APPROVED"),
                GitHubResponses.PullRequest("DRAFT", 4, reviews: approval, isDraft: true),
                GitHubResponses.PullRequest("CONFLICTING", 5, reviews: approval, mergeable: "CONFLICTING"),
            ],
            _ => [],
        });

        // Act
        var result = await fixture.Driver.ListInboxAsync(state: "open");

        // Assert
        Assert.Equal(["CANDIDATE"], MergeStateLookupIds(fixture));
        var mergeStateStatusById = AssertOk<InboxResponse>(result).Prs.ToDictionary(pr => pr.Id, pr => pr.MergeStateStatus);
        Assert.Equal("CLEAN", mergeStateStatusById["CANDIDATE"]);
        Assert.All(mergeStateStatusById.Where(entry => entry.Key != "CANDIDATE"), entry => Assert.Equal("UNKNOWN", entry.Value));
    }

    [Fact]
    public async Task Looks_up_merge_state_ten_pull_requests_at_a_time_and_not_at_all_when_none_could_need_it()
    {
        // Arrange
        await using var fixture = new Fixture();
        var approval = GitHubResponses.ApprovedBy("pelto", "2026-11-20T08:01:49Z");
        var searchHits = Enumerable.Range(1, 25).Select(number => GitHubResponses.PullRequest($"PR{number}", number, reviews: approval)).ToArray();
        fixture.Programs.Respond = GitHubResponses.Answering(searchQuery =>
            searchQuery.Contains("is:open", StringComparison.Ordinal) && searchQuery.EndsWith(" author:@me", StringComparison.Ordinal) ? searchHits : []);

        // Act
        await fixture.Driver.ListInboxAsync(state: "open");

        // Assert
        var lookups = fixture.Programs.RequestsTo("gh").Select(GitHubResponses.IdsAskedFor).Where(ids => ids.Count > 0).ToList();
        Assert.Equal([5, 10, 10], lookups.Select(ids => ids.Count).Order());
        Assert.Equal(25, lookups.SelectMany(ids => ids).Distinct().Count());
    }

    [Fact]
    public async Task Does_not_ask_for_merge_state_when_no_pull_request_could_need_it()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.Programs.Respond = GitHubResponses.Answering(searchQuery =>
            searchQuery.Contains("is:open", StringComparison.Ordinal) ? [GitHubResponses.PullRequest("A", 1)] : []);

        // Act
        await fixture.Driver.ListInboxAsync(state: "open");

        // Assert
        Assert.Empty(MergeStateLookupIds(fixture));
        Assert.DoesNotContain(fixture.Programs.RequestsTo("gh").SelectMany(request => request.Arguments),
            argument => argument.Contains("mergeStateStatus", StringComparison.Ordinal) && argument.Contains("search(", StringComparison.Ordinal));
    }

    private static List<string> MergeStateLookupIds(Fixture fixture) =>
        fixture.Programs.RequestsTo("gh").SelectMany(GitHubResponses.IdsAskedFor).ToList();

    private static List<string> SearchQueries(Fixture fixture) =>
        fixture.Programs.RequestsTo("gh")
            .SelectMany(request => request.Arguments)
            .Where(argument => argument.StartsWith("q=", StringComparison.Ordinal))
            .Select(argument => argument[2..] + " ")
            .ToList();
}
