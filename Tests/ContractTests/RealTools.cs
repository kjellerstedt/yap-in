using System.Text.RegularExpressions;
using YapIn.Infrastructure.Processes;

namespace YapIn.ContractTests;

/// <summary>
/// The real programs, run for real. Contract tests change nothing: they read, ask a program for its own help, or
/// compile a script. A program that is not here is skipped with the reason, never passed.
/// </summary>
internal static partial class RealTools
{
    public static readonly IProcessRunner Runner = new SystemProcessRunner();

    public static void SkipUnlessOnPath(string program)
    {
        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Any(directory => directory.Length > 0 && File.Exists(Path.Combine(directory, program)));
        if (onPath == false) Assert.Skip($"{program} is not installed (not on PATH)");
    }

    public static void SkipUnlessInstalled(string applicationPath)
    {
        if (Directory.Exists(applicationPath) == false) Assert.Skip($"{applicationPath} is not installed");
    }

    /// <summary>Runs a program that must succeed; skips when it fails for want of a precondition (not signed in, not running).</summary>
    public static async Task<string> RunOrSkipAsync(string precondition, string program, params string[] arguments)
    {
        var result = await RunAsync(program, arguments);
        if (result.Succeeded == false) Assert.Skip($"{precondition} ({program} {string.Join(' ', arguments)} exited with {result.ExitCode})");
        return result.StandardOutput;
    }

    public static async Task<ProcessResult> RunAsync(string program, IReadOnlyList<string> arguments) =>
        await Runner.RunAsync(new ProcessRequest(program, arguments, TimeSpan.FromMinutes(2)), TestContext.Current.CancellationToken);

    /// <summary>
    /// Asserts the program's own help names every <c>--flag</c> we pass it. Help is where a program says what it accepts,
    /// so a renamed or removed flag fails here, naming the flag.
    /// </summary>
    public static async Task AssertHelpKnowsFlagsAsync(string program, IReadOnlyList<string> helpArguments, IReadOnlyList<string> ourArguments)
    {
        var help = await RunAsync(program, helpArguments);
        Assert.True(help.Succeeded, $"{program} {string.Join(' ', helpArguments)} exited with {help.ExitCode}: {help.StandardError}");
        var helpText = help.StandardOutput + help.StandardError;
        foreach (var flag in ourArguments.Where(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument.Length > 2))
            Assert.True(FlagIn(flag).IsMatch(helpText), $"{program} {string.Join(' ', helpArguments)} no longer mentions {flag}. Its help says:\n{helpText}");
    }

    private static Regex FlagIn(string flag) => new($@"(?<![\w-]){Regex.Escape(flag)}(?![\w-])");
}
