using System.Diagnostics;
using System.Text;
using YapIn.Domain.Exceptions;

namespace YapIn.Infrastructure.Processes;

/// <summary>A program to run with its arguments passed one by one; nothing goes through a shell.</summary>
internal sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout, string? WorkingDirectory = null);

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// The boundary between our use of programs and the operating system. Implementations only start the program and
/// collect its output; any decision about the output belongs in the caller, which is where narrow integration tests
/// can reach it.
/// </summary>
/// <remarks>Throws <see cref="Domain.Exceptions.ExternalToolFailedException"/> when the program cannot start or times out.</remarks>
internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

internal sealed class SystemProcessRunner : IProcessRunner
{
    // gh search pages are a few MB at most; anything beyond this is not output we expect.
    private const int MaxOutputCharacters = 32 * 1024 * 1024;

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory ?? "",
        };
        foreach (var argument in request.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (process.Start() == false) throw new ExternalToolFailedException(request.FileName, "did not start");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new ExternalToolFailedException(request.FileName, "could not be started");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        var standardOutput = ReadCappedAsync(process.StandardOutput, timeout.Token);
        var standardError = ReadCappedAsync(process.StandardError, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            process.Kill(entireProcessTree: true);
            throw new ExternalToolFailedException(request.FileName, "timed out");
        }
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaxOutputCharacters) throw new ExternalToolFailedException("A program", "printed more output than allowed");
            output.Append(buffer, 0, read);
        }
        return output.ToString();
    }
}
