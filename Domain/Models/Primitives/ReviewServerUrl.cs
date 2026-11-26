using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YapIn.Domain.Models.Primitives;

/// <summary>
/// The local address of a review page a code review tool started (e.g. diffo's http://localhost:4949). The page links to
/// it, so anything else the tool might print, such as another host or a script URL, is never handed on.
/// </summary>
public sealed partial record ReviewServerUrl
{
    public string Value { get; }

    private ReviewServerUrl(string value) => Value = value;

    // Review tools print their address within the first few lines; nothing past this is scanned.
    private const int MaxScannedCharacters = 64 * 1024;

    public static bool TryFindIn(string? toolOutput, [NotNullWhen(true)] out ReviewServerUrl? reviewServerUrl)
    {
        reviewServerUrl = null;
        if (toolOutput is null) return false;
        var scanned = toolOutput.Length > MaxScannedCharacters ? toolOutput[..MaxScannedCharacters] : toolOutput;
        var match = LocalhostAddress().Match(scanned);
        if (match.Success == false) return false;
        var port = int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture);
        if (port is < 1 or > 65535) return false;
        reviewServerUrl = new ReviewServerUrl(match.Value);
        return true;
    }

    public override string ToString() => Value;

    // The path, if any, is limited to URL-safe characters so the link cannot carry markup or a different scheme.
    [GeneratedRegex(@"http://localhost:(?<port>[0-9]{1,5})(?:/[A-Za-z0-9._~/?#=&%-]{0,200})?", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex LocalhostAddress();
}
