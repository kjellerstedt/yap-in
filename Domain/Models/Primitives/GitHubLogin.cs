using System.Text.RegularExpressions;

namespace YapIn.Domain.Models.Primitives;

/// <summary>A GitHub user or organisation login (e.g. "acme").</summary>
public sealed partial record GitHubLogin
{
    private const int MaxLength = 39;

    public string Value { get; }

    public GitHubLogin(string value)
    {
        if (IsValid(value) == false) throw new ArgumentException("Not a GitHub login.", nameof(value));
        Value = value;
    }

    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && AllowedCharacters().IsMatch(value);

    public override string ToString() => Value;

    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\z")]
    private static partial Regex AllowedCharacters();
}
