using System.Text.RegularExpressions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.Adapters.Diffo;

/// <param name="WorktreesDirectory">Where diffo makes its pull request checkouts (~/.diffo/worktrees).</param>
public sealed record DiffoOptions(string WorktreesDirectory);

/// <summary>diffo's CLI, run through npx so no install is needed. The agent attaches through diffo's /diffo skill.</summary>
internal sealed partial class DiffoCodeReviewTool(IProcessRunner processRunner, DiffoOptions options) : ICodeReviewTool
{
    // Pinned so npx never runs a newly published version unseen: it runs in your clone with your credentials.
    // To upgrade, change it here and run the contract tests.
    internal const string PinnedPackage = "@diffohq/diffo@0.35.0";

    private static readonly TimeSpan DiffoTimeout = TimeSpan.FromMinutes(3);

    internal static string[] StartArguments(PullRequestUrl pullRequestUrl) => ["-y", PinnedPackage, "pr", pullRequestUrl.Value, "--no-open"];

    public string AgentPrompt(PullRequestUrl pullRequestUrl) => $"/diffo {pullRequestUrl}";

    // diffo exits non-zero when it needs a choice (e.g. your own pull request); what it printed is still the answer.
    public async Task<CodeReviewStarted> StartAsync(PullRequestUrl pullRequestUrl, string workingDirectory, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest("npx", StartArguments(pullRequestUrl), DiffoTimeout, workingDirectory),
            cancellationToken);
        var output = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        ReviewServerUrl.TryFindIn(output, out var reviewPageUrl);
        return new CodeReviewStarted(reviewPageUrl, FindOwnCheckout(output), output);
    }

    // Only a directory inside diffo's own worktrees is vouched for as where an agent may run.
    private string? FindOwnCheckout(string diffoOutput)
    {
        var match = CheckedOutAt().Match(diffoOutput);
        if (match.Success == false) return null;
        var path = Path.GetFullPath(match.Groups["path"].Value);
        var worktreesDirectory = Path.GetFullPath(options.WorktreesDirectory) + Path.DirectorySeparatorChar;
        return path.StartsWith(worktreesDirectory, StringComparison.Ordinal) && Directory.Exists(path) ? path : null;
    }

    [GeneratedRegex("checked out at `(?<path>/[^`\n]{1,500})`")]
    private static partial Regex CheckedOutAt();
}
