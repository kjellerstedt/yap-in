using YapIn.Domain.Models.Primitives;

namespace YapIn.Infrastructure.Adapters.Orca;

/// <summary>
/// The orca CLI commands we run, built in one place so the adapter and the contract tests send the same arguments.
/// The contract tests check them against the real orca's own help.
/// </summary>
internal static class OrcaCommands
{
    // Orca's "PR Review" workspace status in the Orca profile this was written against.
    public const string PullRequestReviewStatus = "status-8";

    public static string[] ListWorktrees { get; } = ["worktree", "ps", "--json"];

    // Setup hooks are skipped: the review tool reviews the pull request in a checkout of its own, so this worktree only
    // gives the session its own directory and the review an entry in Orca's sidebar.
    public static string[] CreateWorktree(string repoId, PullRequestUrl pullRequestUrl, string branch) =>
        ["worktree", "create", "--repo", $"id:{repoId}", "--name", $"pr-{pullRequestUrl.Number}-review", "--branch", branch,
         "--setup", "skip", "--pr", pullRequestUrl.Number.ToString(), "--no-parent", "--comment", $"Review of {pullRequestUrl}", "--json"];

    public static string[] SetWorkspaceStatus(string worktreeSelector) =>
        ["worktree", "set", "--worktree", worktreeSelector, "--workspace-status", PullRequestReviewStatus, "--json"];

    public static string[] LaunchAgent(string agent, string worktreeSelector, string prompt) =>
        ["agent", "launch", "--agent", agent, "--worktree", worktreeSelector, "--prompt", prompt, "--focus", "--json"];
}
