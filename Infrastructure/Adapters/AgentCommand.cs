using System.Text.RegularExpressions;

namespace YapIn.Infrastructure.Adapters;

/// <summary>
/// The agent a session host starts (<c>Reviews:Agent</c>, default "claude"): Orca's agent id and the terminal command.
/// It becomes a program argument and, in a terminal, a command, so only a plain name is accepted.
/// </summary>
public sealed partial record AgentCommand
{
    public static readonly AgentCommand Default = new("claude");

    public string Name { get; }

    public AgentCommand(string name)
    {
        if (PlainName().IsMatch(name) == false) throw new ArgumentException("An agent is a plain name like \"claude\".", nameof(name));
        Name = name;
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9-]{0,39}\z")]
    private static partial Regex PlainName();
}
