namespace YapIn.Domain.Exceptions;

/// <summary>
/// A program the inbox relies on (gh, diffo, orca, osascript) could not run or failed. <see cref="Exception.Message"/>
/// says which and how, in our words. <see cref="ToolOutput"/> is the program's own error text: for the server log only,
/// since it can hold anything (paths, tokens).
/// </summary>
public sealed class ExternalToolFailedException(string tool, string reason, string? toolOutput = null) : Exception($"{tool} {reason}")
{
    public string Tool { get; } = tool;
    public string? ToolOutput { get; } = toolOutput;
}
