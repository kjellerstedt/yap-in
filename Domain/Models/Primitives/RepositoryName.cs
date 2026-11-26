using System.Text.RegularExpressions;

namespace YapIn.Domain.Models.Primitives;

/// <summary>A repository's name without its owner, as GitHub allows it (e.g. "search").</summary>
public sealed partial record RepositoryName
{
    private const int MaxLength = 100;

    public string Value { get; }

    public RepositoryName(string value)
    {
        if (IsValid(value) == false) throw new ArgumentException("Not a GitHub repository name.", nameof(value));
        Value = value;
    }

    // "." and ".." are excluded: the name becomes a directory under ~/repos.
    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && value is not ("." or "..") && AllowedCharacters().IsMatch(value);

    public override string ToString() => Value;

    [GeneratedRegex(@"\A[A-Za-z0-9._-]+\z")]
    private static partial Regex AllowedCharacters();
}
