using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YapIn.IntegrationTests.Fakes;
using YapIn.Web.Http;

namespace YapIn.IntegrationTests.Host;

public sealed class ApiRequestLogTests
{
    private static readonly string ApiRequestLogCategory = typeof(ApiRequestLog).FullName!;

    [Fact]
    public async Task Logs_when_an_api_request_arrives_and_when_it_finishes_with_its_status()
    {
        // Arrange
        using var server = new HostFixture();
        server.Programs.Respond = GitHubResponses.Answering(_ => []);

        // Act
        using var response = await server.CreatePageClient().GetAsync("/api/prs?state=open", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = ApiRequestMessages(server);
        Assert.Equal(2, messages.Count);
        Assert.Equal("API request: GET /api/prs?state=open", messages[0]);
        Assert.Matches(@"^API request finished: GET /api/prs\?state=open returned 200 in \d+ ms$", messages[1]);
    }

    [Fact]
    public async Task Logs_the_status_of_a_request_the_localhost_guard_refuses()
    {
        // Arrange
        using var server = new HostFixture();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/prs");
        request.Headers.Host = "evil.example";

        // Act
        using var response = await server.CreatePageClient().SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
        Assert.Matches(@"returned 421 in \d+ ms$", ApiRequestMessages(server).Last());
    }

    [Fact]
    public async Task Leaves_static_files_and_ASP_NET_Core_own_information_logs_out()
    {
        // Arrange
        using var server = new HostFixture();
        server.Programs.Respond = GitHubResponses.Answering(_ => []);
        var client = server.CreatePageClient();

        // Act
        await client.GetAsync("/index.html", TestContext.Current.CancellationToken);
        await client.GetAsync("/api/prs", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(ApiRequestMessages(server), message => message.Contains("index.html", StringComparison.Ordinal));
        Assert.DoesNotContain(server.Logs.Entries, entry =>
            entry.Category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) && entry.Level < LogLevel.Warning);
    }

    private static List<string> ApiRequestMessages(HostFixture server) =>
        server.Logs.Entries.Where(entry => entry.Category == ApiRequestLogCategory).Select(entry => entry.Message).ToList();
}
