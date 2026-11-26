using Microsoft.Extensions.DependencyInjection;
using YapIn.Domain.Interfaces;
using YapIn.Infrastructure.Adapters.Diffo;
using YapIn.Infrastructure.Adapters.ITerm;
using YapIn.Infrastructure.Adapters.MacTerminal;
using YapIn.Infrastructure.Adapters.Orca;

namespace YapIn.Web.Startup;

/// <param name="Name">What the setting holds, and what <c>IAgentSessionHost.Name</c> says for a session host.</param>
/// <param name="Label">How setup describes it.</param>
/// <param name="Register">Puts its adapter in place of the role's interface.</param>
/// <param name="SetupNote">What setup prints once it is chosen, such as how to install it.</param>
internal sealed record ToolOption(string Name, string Label, Action<IServiceCollection> Register, string? SetupNote = null);

/// <summary>Every tool setup can choose between. One entry adds a tool; setup and the wiring both read this list.</summary>
internal static class ToolOptions
{
    public static readonly IReadOnlyList<ToolOption> SessionHosts =
    [
        new("orca", "Orca: a worktree per pull request", services => services.AddSingleton<IAgentSessionHost, OrcaSessionHost>()),
        new("iterm", "iTerm: a new window in the local clone", services => services.AddSingleton<IAgentSessionHost, ITermSessionHost>()),
        new("terminal", "macOS Terminal: a new window in the local clone", services => services.AddSingleton<IAgentSessionHost, MacTerminalSessionHost>()),
    ];

    /// <summary>The review tool that is no tool: the Review button links to the pull request's changes on GitHub.</summary>
    public const string GitHubReviewTool = "github";

    public static readonly IReadOnlyList<ToolOption> ReviewTools =
    [
        new(GitHubReviewTool, "Open the pull request's changes on GitHub", _ => { }),
        new("diffo", "Start a diffo review with an agent session attached", services =>
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            services.AddSingleton(new DiffoOptions(Path.Combine(homeDirectory, ".diffo", "worktrees")));
            services.AddSingleton<ICodeReviewTool, DiffoCodeReviewTool>();
        }, SetupNote: $"""
            diffo runs through npx, so it needs Node with npm (https://nodejs.org). The agent attaches to the review with
            diffo's /diffo skill; install it for Claude Code and your other coding agents once with:

                npx -y {DiffoCodeReviewTool.PinnedPackage} setup

            More about diffo: https://github.com/DiffoHQ/diffo
            """),
    ];

    public static ToolOption Find(IReadOnlyList<ToolOption> options, string name, string role) =>
        options.FirstOrDefault(option => option.Name == name)
        ?? throw new InvalidOperationException($"Unknown {role} \"{name}\". Choose one of: {string.Join(", ", options.Select(option => option.Name))}");
}
