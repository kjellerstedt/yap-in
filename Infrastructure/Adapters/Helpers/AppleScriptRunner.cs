using YapIn.Domain.Exceptions;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.Adapters.Helpers;

/// <summary>
/// Runs an AppleScript through osascript. Values reach the script only as arguments to its <c>run</c> handler
/// (<c>on run argv</c>), where AppleScript's <c>quoted form of</c> shell-quotes them, so none is ever pasted into script
/// or shell text.
/// </summary>
internal static class AppleScriptRunner
{
    private static readonly TimeSpan OsascriptTimeout = TimeSpan.FromSeconds(30);

    public static async Task RunAsync(
        IProcessRunner processRunner, IReadOnlyList<string> scriptLines, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var osascriptArguments = scriptLines.SelectMany(line => new[] { "-e", line }).Concat(arguments).ToList();
        var result = await processRunner.RunAsync(new ProcessRequest("osascript", osascriptArguments, OsascriptTimeout), cancellationToken);
        if (result.Succeeded == false) throw new ExternalToolFailedException("osascript", $"exited with {result.ExitCode}", result.StandardError);
    }
}
