using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

/// <summary>
/// Somewhere an agent session can run: a workspace manager (Orca) or a terminal (iTerm, macOS Terminal). Chosen in
/// setup; throws ExternalToolFailedException when its program fails.
/// </summary>
public interface IAgentSessionHost
{
    /// <summary>How the host is named in setup and in the <c>Reviews:SessionHost</c> setting, e.g. "orca".</summary>
    string Name { get; }

    /// <summary>
    /// The workspace a review of <paramref name="pullRequestUrl"/> would run in, or <c>null</c> when this host cannot
    /// provide one. A host with workspaces of its own makes one; a terminal runs in <paramref name="localClone"/>.
    /// </summary>
    Task<AgentWorkspace?> PrepareWorkspaceAsync(PullRequestUrl pullRequestUrl, string? localClone, CancellationToken cancellationToken);

    Task LaunchAgentAsync(AgentWorkspace workspace, string prompt, CancellationToken cancellationToken);
}
