using System.Text.Json;
using YapIn.IntegrationTests.Fakes;
using YapIn.Tests.Shared;

namespace YapIn.IntegrationTests.Host;

/// <summary>The JSON the page actually receives: field names, nulls and relation names as the page reads them.</summary>
public sealed class ApiJsonTests
{
    [Fact]
    public async Task Writes_the_inbox_as_the_page_reads_it()
    {
        // Arrange
        using var server = new HostFixture();
        server.Programs.Respond = GitHubResponses.Answering(searchQuery =>
            searchQuery.EndsWith(" author:@me", StringComparison.Ordinal) && searchQuery.Contains("is:open", StringComparison.Ordinal)
                ? [GitHubResponses.PullRequest("A", 1)]
                : []);

        // Act
        var body = await server.CreatePageClient().GetStringAsync("/api/prs", TestContext.Current.CancellationToken);

        // Assert
        using var json = JsonDocument.Parse(body);
        var pr = json.RootElement.GetProperty("prs")[0];
        Assert.Equal("authored", pr.GetProperty("relations")[0].GetString());
        Assert.Equal("OPEN", pr.GetProperty("state").GetString());
        Assert.Equal("2026-11-20T09:00:00Z", pr.GetProperty("updatedAt").GetString());
        Assert.Equal(JsonValueKind.Null, pr.GetProperty("myReview").ValueKind);
        Assert.Equal(JsonValueKind.Null, pr.GetProperty("stack").ValueKind);
        Assert.Equal(1, pr.GetProperty("comments").GetProperty("bot").GetInt32());
    }

    [Theory]
    [InlineData("diffo")]
    [InlineData("github")]
    public async Task Tells_the_page_what_its_review_button_does(string reviewTool)
    {
        // Arrange
        using var server = new HostFixture(reviewTool);

        // Act
        var body = await server.CreatePageClient().GetStringAsync("/api/review-tool", TestContext.Current.CancellationToken);

        // Assert
        using var json = JsonDocument.Parse(body);
        Assert.Equal(reviewTool, json.RootElement.GetProperty("reviewTool").GetString());
    }

    [Fact]
    public async Task Starts_no_review_when_the_review_button_opens_github()
    {
        // Arrange
        using var server = new HostFixture("github");

        // Act
        using var response = await server.CreatePageClient().PostAsync(
            "/api/reviews?url=https://github.com/acme/search/pull/1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Empty(server.Programs.RequestsTo("npx"));
    }

    [Fact]
    public async Task Writes_errors_as_an_error_field()
    {
        // Arrange
        using var server = new HostFixture();

        // Act
        using var response = await server.CreatePageClient().PostAsync("/api/reviews?url=nope", null, TestContext.Current.CancellationToken);

        // Assert
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Not a GitHub pull request URL", json.RootElement.GetProperty("error").GetString());
    }
}
