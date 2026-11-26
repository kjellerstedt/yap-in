using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YapIn.Domain.Models.Primitives;

/// <summary>
/// The address of a pull request on github.com, and nothing else: no query, fragment, other host or trailing path.
/// It reaches program arguments (the review tool, the session host, the agent's prompt), so only this exact shape is accepted.
/// </summary>
public sealed partial record PullRequestUrl
{
    // Owner (39) + repository (100) + fixed parts and a 10-digit number fit well within this.
    private const int MaxLength = 200;

    public GitHubLogin Owner { get; }
    public RepositoryName Repository { get; }
    public PullRequestNumber Number { get; }
    public string Value { get; }

    private PullRequestUrl(GitHubLogin owner, RepositoryName repository, PullRequestNumber number)
    {
        Owner = owner;
        Repository = repository;
        Number = number;
        Value = $"https://github.com/{owner}/{repository}/pull/{number}";
    }

    public static bool TryParse(string? raw, [NotNullWhen(true)] out PullRequestUrl? pullRequestUrl)
    {
        pullRequestUrl = null;
        // Length first, so the pattern never runs on oversized input.
        if (raw is null || raw.Length > MaxLength) return false;
        var match = Shape().Match(raw);
        if (match.Success == false) return false;

        var owner = match.Groups["owner"].Value;
        var repository = match.Groups["repository"].Value;
        if (GitHubLogin.IsValid(owner) == false || RepositoryName.IsValid(repository) == false) return false;
        if (int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) == false || number < 1)
            return false;

        pullRequestUrl = new PullRequestUrl(new GitHubLogin(owner), new RepositoryName(repository), new PullRequestNumber(number));
        return true;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"\Ahttps://github\.com/(?<owner>[^/]+)/(?<repository>[^/]+)/pull/(?<number>[0-9]{1,10})\z")]
    private static partial Regex Shape();
}
