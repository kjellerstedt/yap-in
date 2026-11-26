namespace YapIn.Tests.Shared;

/// <summary>
/// Rows of `orca worktree ps --json`, as the fakes in the integration tests answer with them. The contract tests check
/// that the real orca still sends every property named here, so these samples cannot drift from the real tool unseen.
/// </summary>
internal static class OrcaWorktreeSamples
{
    public const string MainCheckoutOfSearch =
        """{"repo":"search","repoId":"r1","isMainWorktree":true,"path":"/repos/search","branch":"refs/heads/main","address":{"worktree":"identity:main"}}""";

    public const string ReviewWorktreeOfSearch =
        """{"repo":"search","repoId":"r1","isMainWorktree":false,"path":"/orca/search/pr-412-review","branch":"refs/heads/pr-review/412","address":{"worktree":"identity:review"}}""";
}
