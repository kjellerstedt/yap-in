using System.Text.Json;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure;
using YapIn.Infrastructure.Adapters;
using YapIn.Infrastructure.Adapters.Orca;
using YapIn.Tests.Shared;

namespace YapIn.ContractTests.Orca;

/// <summary>Against the real orca CLI. Orca is not ours: these are where its changes show up first.</summary>
public sealed class OrcaContractTests
{
    private static readonly PullRequestUrl SomePullRequest = Parse("https://github.com/acme/yap-in-contract-test-no-such-repo/pull/1");

    [Fact]
    public async Task Lists_worktrees_in_the_shape_the_adapter_reads()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("orca");
        var output = await RealTools.RunOrSkipAsync("Orca is not running", "orca", OrcaCommands.ListWorktrees);

        // Act
        var list = JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.OrcaWorkspaceList);

        // Assert
        Assert.NotNull(list);
        var mainCheckouts = list.Result.Worktrees.Where(row => row.IsMainWorktree).ToList();
        Assert.NotEmpty(mainCheckouts);
        Assert.All(mainCheckouts, row =>
        {
            Assert.False(string.IsNullOrEmpty(row.Repo), "a main worktree row has no repo");
            Assert.False(string.IsNullOrEmpty(row.RepoId), "a main worktree row has no repoId");
            Assert.False(string.IsNullOrEmpty(row.Path), "a main worktree row has no path");
            Assert.False(string.IsNullOrEmpty(row.Branch), "a main worktree row has no branch");
            Assert.False(string.IsNullOrEmpty(row.Address?.Worktree), "a main worktree row has no address.worktree");
        });
    }

    [Fact]
    public async Task Still_sends_every_property_the_integration_tests_fake_answers_with()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("orca");
        var output = await RealTools.RunOrSkipAsync("Orca is not running", "orca", OrcaCommands.ListWorktrees);
        using var real = JsonDocument.Parse(output);
        var realRow = real.RootElement.GetProperty("result").GetProperty("worktrees").EnumerateArray().First(row => row.GetProperty("isMainWorktree").GetBoolean());
        var realPaths = PropertyPaths(realRow).ToHashSet();

        // Act
        var missing = new[] { OrcaWorktreeSamples.MainCheckoutOfSearch, OrcaWorktreeSamples.ReviewWorktreeOfSearch }
            .SelectMany(sample => { using var document = JsonDocument.Parse(sample); return PropertyPaths(document.RootElement).ToList(); })
            .Distinct().Where(path => realPaths.Contains(path) == false).ToList();

        // Assert
        Assert.True(missing.Count == 0, $"The fakes answer with properties orca no longer sends: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task Answers_for_a_repo_it_does_not_know_with_no_workspace_and_changes_nothing()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("orca");
        await RealTools.RunOrSkipAsync("Orca is not running", "orca", OrcaCommands.ListWorktrees);

        // Act
        var workspace = await new OrcaSessionHost(RealTools.Runner, AgentCommand.Default)
            .PrepareWorkspaceAsync(SomePullRequest, localClone: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(workspace);
    }

    [Theory]
    [MemberData(nameof(CommandsWeSend))]
    public async Task Knows_every_flag_we_send_in_its_own_help(string[] ourArguments)
    {
        // Arrange
        RealTools.SkipUnlessOnPath("orca");

        // Act and assert
        await RealTools.AssertHelpKnowsFlagsAsync("orca", [ourArguments[0], ourArguments[1], "--help"], ourArguments);
    }

    public static TheoryData<string[]> CommandsWeSend() => new()
    {
        OrcaCommands.CreateWorktree("repo-id", SomePullRequest, "pr-review/1"),
        OrcaCommands.SetWorkspaceStatus("identity:worktree"),
        OrcaCommands.LaunchAgent("claude", "identity:worktree", "prompt"),
        OrcaCommands.ListWorktrees,
    };

    // "address.worktree" for {"address":{"worktree":…}}: every property, nested ones joined with a dot.
    private static IEnumerable<string> PropertyPaths(JsonElement element, string prefix = "")
    {
        if (element.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in element.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            yield return path;
            foreach (var nested in PropertyPaths(property.Value, path)) yield return nested;
        }
    }

    private static PullRequestUrl Parse(string url) => PullRequestUrl.TryParse(url, out var pullRequestUrl) ? pullRequestUrl : throw new InvalidOperationException(url);
}
