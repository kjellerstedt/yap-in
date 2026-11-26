using System.Net;

namespace YapIn.IntegrationTests.Host;

public sealed class LocalhostOnlyTests
{
    [Theory]
    [InlineData("evil.example")]
    [InlineData("evil.example:4477")]
    [InlineData("localhost.evil.example")]
    [InlineData("127.0.0.1.nip.io")]
    public async Task Refuses_requests_addressed_to_another_host_name(string host)
    {
        // Arrange
        using var server = new HostFixture();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/prs");
        request.Headers.Host = host;

        // Act
        using var response = await server.CreatePageClient().SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
        Assert.Empty(server.Programs.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost:5173")]
    [InlineData("null")]
    public async Task Refuses_to_start_a_review_for_another_origin(string? origin)
    {
        // Arrange
        using var server = new HostFixture();
        using var client = server.CreatePageClient();
        client.DefaultRequestHeaders.Remove("Origin");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);

        // Act
        using var response = await client.PostAsync("/api/reviews?url=https://github.com/acme/search/pull/1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(server.Programs.Requests);
    }

    [Fact]
    public async Task Lets_the_page_itself_through()
    {
        // Arrange
        using var server = new HostFixture();

        // Act
        using var response = await server.CreatePageClient().PostAsync("/api/reviews?url=not-a-pull-request", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
