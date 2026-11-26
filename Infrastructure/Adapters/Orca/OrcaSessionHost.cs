using System.Text.Json;
using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.Adapters.Orca;

internal sealed record OrcaWorkspaceList(OrcaWorkspaceListResult Result);

internal sealed record OrcaWorkspaceListResult(IReadOnlyList<OrcaWorkspaceRow> Worktrees);

internal sealed record OrcaWorkspaceRow(string? Repo, string? RepoId, bool IsMainWorktree, string? Path, string? Branch, OrcaAddress? Address);

internal sealed record OrcaAddress(string? Worktree);

/// <summary>
/// Orca, through its CLI: one worktree per pull request, made from the repo's main checkout in Orca and reused when the
/// review is started again. A repo Orca does not know gets no workspace, so the review cannot start there.
/// </summary>
internal sealed class OrcaSessionHost(IProcessRunner processRunner, AgentCommand agentCommand) : IAgentSessionHost
{
    private static readonly TimeSpan OrcaTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WorktreeCreateTimeout = TimeSpan.FromMinutes(2);

    public string Name => "orca";

    public async Task<AgentWorkspace?> PrepareWorkspaceAsync(PullRequestUrl pullRequestUrl, string? localClone, CancellationToken cancellationToken)
    {
        var mainCheckout = (await ListAsync(cancellationToken)).FirstOrDefault(row =>
            row.IsMainWorktree && row.Repo == pullRequestUrl.Repository.Value && row.RepoId is not null);
        if (mainCheckout is null) return null;

        var branch = $"pr-review/{pullRequestUrl.Number}";
        var existing = await FindReviewWorktreeAsync(pullRequestUrl.Repository, branch, cancellationToken);
        if (existing is not null) return existing;

        await RunOrcaAsync(OrcaCommands.CreateWorktree(mainCheckout.RepoId!, pullRequestUrl, branch), WorktreeCreateTimeout, cancellationToken);
        var created = await FindReviewWorktreeAsync(pullRequestUrl.Repository, branch, cancellationToken)
            ?? throw new ExternalToolFailedException("orca", $"created no worktree on {branch}");
        await RunOrcaAsync(OrcaCommands.SetWorkspaceStatus(created.HostWorkspaceId!), OrcaTimeout, cancellationToken);
        return created;
    }

    public Task LaunchAgentAsync(AgentWorkspace workspace, string prompt, CancellationToken cancellationToken)
    {
        var worktreeSelector = workspace.HostWorkspaceId ?? throw new ArgumentException("Orca launches only in a worktree it made.", nameof(workspace));
        return RunOrcaAsync(OrcaCommands.LaunchAgent(agentCommand.Name, worktreeSelector, prompt), OrcaTimeout, cancellationToken);
    }

    private async Task<AgentWorkspace?> FindReviewWorktreeAsync(RepositoryName repository, string branch, CancellationToken cancellationToken)
    {
        var row = (await ListAsync(cancellationToken)).FirstOrDefault(row =>
            row.Repo == repository.Value && row.Branch == $"refs/heads/{branch}" && row.Path is not null && row.Address?.Worktree is not null);
        return row is null ? null : new AgentWorkspace(row.Path!, row.Address!.Worktree!);
    }

    private async Task<IReadOnlyList<OrcaWorkspaceRow>> ListAsync(CancellationToken cancellationToken)
    {
        var output = await RunOrcaAsync(OrcaCommands.ListWorktrees, OrcaTimeout, cancellationToken);
        return JsonSerializer.Deserialize(output, InfrastructureJsonContext.Default.OrcaWorkspaceList)?.Result.Worktrees
            ?? throw new ExternalToolFailedException("orca", "listed no workspaces");
    }

    private async Task<string> RunOrcaAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(new ProcessRequest("orca", arguments, timeout), cancellationToken);
        return result.Succeeded
            ? result.StandardOutput
            : throw new ExternalToolFailedException("orca", $"{arguments[0]} {arguments[1]} exited with {result.ExitCode}", result.StandardError);
    }
}
