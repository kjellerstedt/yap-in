using YapIn.Api.Responses;
using YapIn.Infrastructure.Processes;
using YapIn.Tests.Shared;
using static YapIn.IntegrationTests.Assertions;

namespace YapIn.IntegrationTests.Endpoints;

public sealed class StartReviewTests
{
    private const string PullRequest = "https://github.com/acme/search/pull/412";
    private const string ClaudePrompt = $"/diffo {PullRequest}";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://github.com/acme/search/pull/412?x=1")]
    [InlineData("https://github.com/acme/search/pull/412';open -a Calculator;'")]
    [InlineData("https://github.com/acme/search/pull/412\n")]
    [InlineData("file:///etc/passwd")]
    public async Task Refuses_anything_but_a_pull_request_address_before_running_anything(string? url)
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.AddClone("search");

        // Act
        var result = await fixture.Driver.StartReviewAsync(url);

        // Assert
        Assert.Equal("Not a GitHub pull request URL", AssertError(result, 400));
        Assert.Empty(fixture.Programs.Requests);
    }

    [Fact]
    public async Task Says_which_repo_has_no_local_clone()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal("No local clone of search", AssertError(result, 404));
        Assert.Empty(fixture.Programs.Requests);
    }

    [Fact]
    public async Task Runs_diffo_in_the_clone_then_claude_in_diffos_checkout_with_values_passed_only_as_arguments()
    {
        // Arrange
        await using var fixture = new Fixture();
        var clonePath = fixture.AddClone("search");
        var diffoCheckout = fixture.AddDiffoCheckout("search", 412);
        fixture.Programs.Respond = DiffoPrints($"watching pull request #412\n→ http://localhost:4949\nnote: It is checked out at `{diffoCheckout}`; run from there");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal(new StartReviewResponse("http://localhost:4949", "iterm", null), AssertOk<StartReviewResponse>(result));

        var diffo = Assert.Single(fixture.Programs.RequestsTo("npx"));
        Assert.Equal(clonePath, diffo.WorkingDirectory);
        Assert.Equal(["-y", "@diffohq/diffo@0.35.0", "pr", PullRequest, "--no-open"], diffo.Arguments);

        var osascript = Assert.Single(fixture.Programs.RequestsTo("osascript"));
        Assert.Equal([diffoCheckout, "claude", ClaudePrompt], osascript.Arguments.TakeLast(3));
        Assert.DoesNotContain(osascript.Arguments.SkipLast(3), argument => argument.Contains(PullRequest, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/tmp")]
    [InlineData("/Users/someone/.diffo/worktrees/../../.ssh")]
    public async Task Starts_claude_in_the_clone_when_diffo_names_a_checkout_outside_its_own_worktrees(string namedCheckout)
    {
        // Arrange
        await using var fixture = new Fixture();
        var clonePath = fixture.AddClone("search");
        fixture.Programs.Respond = DiffoPrints($"→ http://localhost:4949\nIt is checked out at `{namedCheckout}`; run from there");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        AssertOk<StartReviewResponse>(result);
        Assert.Equal(clonePath, Assert.Single(fixture.Programs.RequestsTo("osascript")).Arguments[^3]);
    }

    [Fact]
    public async Task Hands_back_what_diffo_said_when_it_printed_no_review_address()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.AddClone("search");
        fixture.Programs.Respond = request => request.FileName == "npx"
            ? FakeProcessRunner.Fails("This is your own pull request: branch review or PR review?")
            : FakeProcessRunner.Succeeds("");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        var started = AssertOk<StartReviewResponse>(result);
        Assert.Null(started.ReviewUrl);
        Assert.Contains("branch review or PR review?", started.ToolOutput, StringComparison.Ordinal);
        Assert.Single(fixture.Programs.RequestsTo("osascript"));
    }

    [Fact]
    public async Task Creates_one_orca_worktree_per_pull_request_and_launches_claude_there()
    {
        // Arrange
        await using var fixture = new Fixture("orca");
        fixture.Programs.Respond = OrcaWithMainCheckoutOfSearch(diffoOutput: "→ http://localhost:5050");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal(new StartReviewResponse("http://localhost:5050", "orca", null), AssertOk<StartReviewResponse>(result));
        Assert.Equal("/orca/search/pr-412-review", Assert.Single(fixture.Programs.RequestsTo("npx")).WorkingDirectory);
        var orcaCalls = fixture.Programs.RequestsTo("orca").Select(request => request.Arguments).ToList();
        Assert.Contains(orcaCalls, arguments => arguments.SequenceEqual(
            ["worktree", "create", "--repo", "id:r1", "--name", "pr-412-review", "--branch", "pr-review/412",
             "--setup", "skip", "--pr", "412", "--no-parent", "--comment", $"Review of {PullRequest}", "--json"]));
        Assert.Contains(orcaCalls, arguments => arguments.SequenceEqual(
            ["worktree", "set", "--worktree", "identity:review", "--workspace-status", "status-8", "--json"]));
        Assert.Contains(orcaCalls, arguments => arguments.SequenceEqual(
            ["agent", "launch", "--agent", "claude", "--worktree", "identity:review", "--prompt", ClaudePrompt, "--focus", "--json"]));
        Assert.Empty(fixture.Programs.RequestsTo("osascript"));
    }

    [Fact]
    public async Task Reuses_the_orca_worktree_when_the_review_is_started_again()
    {
        // Arrange
        await using var fixture = new Fixture("orca");
        fixture.Programs.Respond = OrcaWithMainCheckoutOfSearch(diffoOutput: "→ http://localhost:5050");

        // Act
        AssertOk<StartReviewResponse>(await fixture.Driver.StartReviewAsync(PullRequest));
        AssertOk<StartReviewResponse>(await fixture.Driver.StartReviewAsync(PullRequest));

        // Assert
        var orcaCalls = fixture.Programs.RequestsTo("orca").Select(request => request.Arguments).ToList();
        Assert.Single(orcaCalls, arguments => arguments.Take(2).SequenceEqual(["worktree", "create"]));
        Assert.Equal(2, orcaCalls.Count(arguments => arguments.Take(2).SequenceEqual(["agent", "launch"])));
    }

    [Fact]
    public async Task Says_when_orca_does_not_know_the_repo_and_starts_nothing()
    {
        // Arrange
        await using var fixture = new Fixture("orca");
        fixture.AddClone("billing");
        fixture.Programs.Respond = OrcaWithMainCheckoutOfSearch(diffoOutput: "→ http://localhost:5050");

        // Act
        var result = await fixture.Driver.StartReviewAsync("https://github.com/acme/billing/pull/7");

        // Assert
        Assert.Equal("orca cannot open billing", AssertError(result, 404));
        Assert.Empty(fixture.Programs.RequestsTo("npx"));
        Assert.DoesNotContain(fixture.Programs.RequestsTo("orca"), request => request.Arguments[0] == "agent");
    }

    [Fact]
    public async Task Tells_the_page_a_fixed_message_when_orca_is_not_running_and_starts_nothing_else()
    {
        // Arrange
        await using var fixture = new Fixture("orca");
        fixture.AddClone("search");
        fixture.Programs.Respond = _ => FakeProcessRunner.Fails("runtime_unavailable at /Users/someone/secret-path");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal("Could not start the review. See the server log.", AssertError(result, 502));
        Assert.Empty(fixture.Programs.RequestsTo("npx"));
    }

    [Fact]
    public async Task Opens_a_macos_terminal_window_in_the_clone_when_that_is_the_chosen_host()
    {
        // Arrange
        await using var fixture = new Fixture("terminal");
        var clonePath = fixture.AddClone("search");
        fixture.Programs.Respond = DiffoPrints("→ http://localhost:4949");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal(new StartReviewResponse("http://localhost:4949", "terminal", null), AssertOk<StartReviewResponse>(result));
        var osascript = Assert.Single(fixture.Programs.RequestsTo("osascript"));
        Assert.Equal([clonePath, "claude", ClaudePrompt], osascript.Arguments.TakeLast(3));
        Assert.Contains(osascript.Arguments, argument => argument.Contains("application \"Terminal\"", StringComparison.Ordinal));
        Assert.DoesNotContain(osascript.Arguments.SkipLast(3), argument => argument.Contains(PullRequest, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tells_the_page_a_fixed_message_when_the_terminal_cannot_start()
    {
        // Arrange
        await using var fixture = new Fixture();
        fixture.AddClone("search");
        fixture.Programs.Respond = request => request.FileName == "osascript"
            ? FakeProcessRunner.Fails("execution error: /Users/someone/secret-path not allowed")
            : FakeProcessRunner.Succeeds("→ http://localhost:4949");

        // Act
        var result = await fixture.Driver.StartReviewAsync(PullRequest);

        // Assert
        Assert.Equal("Could not start the review. See the server log.", AssertError(result, 502));
    }

    private static Func<ProcessRequest, ProcessResult> DiffoPrints(string output) => request =>
        request.FileName == "npx" ? FakeProcessRunner.Succeeds(output) : FakeProcessRunner.Succeeds("");

    // Orca knows the search repo's main checkout; the review worktree appears once `worktree create` has run.
    private static Func<ProcessRequest, ProcessResult> OrcaWithMainCheckoutOfSearch(string diffoOutput)
    {
        var reviewWorktreeCreated = false;
        return request =>
        {
            if (request.FileName == "npx") return FakeProcessRunner.Succeeds(diffoOutput);
            if (request.FileName != "orca") return FakeProcessRunner.Succeeds("");
            if (request.Arguments.Take(2).SequenceEqual(["worktree", "create"])) reviewWorktreeCreated = true;
            var rows = reviewWorktreeCreated
                ? $"{OrcaWorktreeSamples.MainCheckoutOfSearch},{OrcaWorktreeSamples.ReviewWorktreeOfSearch}"
                : OrcaWorktreeSamples.MainCheckoutOfSearch;
            return FakeProcessRunner.Succeeds("""{"result":{"worktrees":[ROWS]}}""".Replace("ROWS", rows, StringComparison.Ordinal));
        };
    }
}
