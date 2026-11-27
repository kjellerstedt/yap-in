using YapIn.Domain.Exceptions;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.GitHub;

/// <summary>How the server runs gh. It reads output the callers parse, so a failed run is an error here, in one place.</summary>
internal static class GhCli
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static Task<ProcessResult> RunAsync(IProcessRunner processRunner, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        processRunner.RunAsync(new ProcessRequest("gh", arguments, Timeout), cancellationToken);

    public static async Task<string> OutputAsync(IProcessRunner processRunner, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(processRunner, arguments, cancellationToken);
        return result.Succeeded ? result.StandardOutput : throw new ExternalToolFailedException("gh", $"exited with {result.ExitCode}", result.StandardError);
    }
}
