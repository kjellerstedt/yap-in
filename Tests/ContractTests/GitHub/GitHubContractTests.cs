using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.GitHub;

namespace YapIn.ContractTests.GitHub;

/// <summary>Against GitHub, through the real gh and your own sign-in. It only reads.</summary>
public sealed class GitHubContractTests
{
    [Fact]
    public async Task Github_accepts_our_search_query_and_answers_in_the_shape_we_read()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("gh");
        await RealTools.RunOrSkipAsync("gh is not signed in", "gh", "auth", "status");
        var search = new GitHubPullRequestSearch(RealTools.Runner, TimeProvider.System, new GitHubViewer(RealTools.Runner));

        // Act
        var inbox = await search.BuildInboxAsync(new InboxQuery(InboxState.Open, LookbackDays.Default), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(inbox.Viewer.Value);
        Assert.All(inbox.PullRequests, pullRequest => Assert.NotEmpty(pullRequest.Title));
    }

    [Fact]
    public async Task Github_answers_by_id_and_by_address_in_the_shape_we_read()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("gh");
        await RealTools.RunOrSkipAsync("gh is not signed in", "gh", "auth", "status");
        var search = new GitHubPullRequestSearch(RealTools.Runner, TimeProvider.System, new GitHubViewer(RealTools.Runner));
        var inbox = await search.BuildInboxAsync(new InboxQuery(InboxState.Open, LookbackDays.Default), TestContext.Current.CancellationToken);
        var pullRequests = inbox.PullRequests.Take(3).ToList();
        if (pullRequests.Count == 0) Assert.Skip("There is no pull request in your inbox to look up");

        // Act
        var refreshed = await search.RefreshPullRequestsAsync(pullRequests, TestContext.Current.CancellationToken);
        var fetched = await search.FetchPullRequestsAsync(pullRequests.Select(pullRequest => pullRequest.Url).ToList(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(pullRequests.Select(pullRequest => pullRequest.Id).Order(), refreshed.Select(pullRequest => pullRequest.Id).Order());
        Assert.Equal(pullRequests.Select(pullRequest => pullRequest.Id).Order(), fetched.Select(pullRequest => pullRequest.Id).Order());
    }

    [Fact]
    public async Task Github_answers_the_activity_feeds_and_an_unchanged_feed_with_a_304()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("gh");
        await RealTools.RunOrSkipAsync("gh is not signed in", "gh", "auth", "status");
        var activity = new GitHubActivity(RealTools.Runner, new GitHubViewer(RealTools.Runner));

        // Act
        var baseline = await activity.CheckForActivityAsync(TestContext.Current.CancellationToken);
        var second = await activity.CheckForActivityAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(baseline.HasChanged);
        Assert.Empty(baseline.NotifiedPullRequests);
        Assert.True(baseline.PollInterval >= TimeSpan.FromSeconds(60));
        Assert.True(second.PollInterval >= TimeSpan.FromSeconds(60)); // reaching here means the 304 was read, not thrown as a failure
    }
}
