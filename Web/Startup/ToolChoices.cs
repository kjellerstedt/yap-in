using Microsoft.Extensions.Configuration;

namespace YapIn.Web.Startup;

/// <summary>
/// The tools this install works through, by the names setup saves: <c>Reviews:ReviewTool</c> and, when that starts
/// agent sessions, <c>Reviews:SessionHost</c>. A setting (appsettings, an environment variable such as
/// <c>Reviews__SessionHost</c>) overrides what setup saved.
/// </summary>
internal sealed record ToolChoices(string? SessionHost, string ReviewTool)
{
    /// <summary>False when the Review button only links to GitHub, so no session host is needed.</summary>
    public bool StartsAgentSessions => ReviewTool != ToolOptions.GitHubReviewTool;

    /// <summary>
    /// The saved choices, asking first when there are none or <paramref name="setupRequested"/> (<c>--setup</c>).
    /// Asking needs a terminal; without one the server stops and says how to set up, rather than guess.
    /// </summary>
    public static ToolChoices Resolve(
        IConfiguration configuration, UserSettingsFile settingsFile, bool setupRequested, SetupPrompt setupPrompt, bool terminalAvailable)
    {
        var saved = settingsFile.Read();
        var sessionHost = configuration["Reviews:SessionHost"] ?? saved?.SessionHost;
        var reviewTool = configuration["Reviews:ReviewTool"] ?? saved?.ReviewTool;
        if (setupRequested == false && reviewTool is not null)
        {
            var configured = new ToolChoices(sessionHost, reviewTool);
            if (configured.StartsAgentSessions == false || sessionHost is not null) return Checked(configured);
        }

        if (terminalAvailable == false)
            throw new InvalidOperationException("No review tool or session host is chosen yet. Run once from a terminal: dotnet run --project Web -- --setup");

        var chosen = setupPrompt.Ask(sessionHost, reviewTool);
        settingsFile.Save(chosen);
        return chosen;
    }

    private static ToolChoices Checked(ToolChoices choices)
    {
        ToolOptions.Find(ToolOptions.ReviewTools, choices.ReviewTool, "review tool");
        if (choices.StartsAgentSessions) ToolOptions.Find(ToolOptions.SessionHosts, choices.SessionHost!, "session host");
        return choices;
    }
}
