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
        var search = new GitHubPullRequestSearch(RealTools.Runner, TimeProvider.System);

        // Act
        var inbox = await search.BuildInboxAsync(new InboxQuery(InboxState.Open, LookbackDays.Default), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(inbox.Viewer.Value);
        Assert.All(inbox.PullRequests, pullRequest => Assert.NotEmpty(pullRequest.Title));
    }
}
