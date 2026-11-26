using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Adapters.Helpers;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.Adapters.MacTerminal;

/// <summary>A new macOS Terminal window running the agent. It has no workspaces of its own, so it runs in the local clone.</summary>
internal sealed class MacTerminalSessionHost(IProcessRunner processRunner, AgentCommand agentCommand) : IAgentSessionHost
{
    /// <summary>Internal so the contract tests can have the real AppleScript compiler check the exact text.</summary>
    internal static readonly string[] Script =
    [
        "on run argv",
        "set sessionDir to item 1 of argv",
        "set agentCommand to item 2 of argv",
        "set agentPrompt to item 3 of argv",
        "tell application \"Terminal\"",
        "activate",
        "do script \"cd \" & quoted form of sessionDir & \" && \" & quoted form of agentCommand & \" \" & quoted form of agentPrompt",
        "end tell",
        "end run",
    ];

    public string Name => "terminal";

    public Task<AgentWorkspace?> PrepareWorkspaceAsync(PullRequestUrl pullRequestUrl, string? localClone, CancellationToken cancellationToken) =>
        Task.FromResult(localClone is null ? null : new AgentWorkspace(localClone, HostWorkspaceId: null));

    public Task LaunchAgentAsync(AgentWorkspace workspace, string prompt, CancellationToken cancellationToken) =>
        AppleScriptRunner.RunAsync(processRunner, Script, [workspace.Directory, agentCommand.Name, prompt], cancellationToken);
}
