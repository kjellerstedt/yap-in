using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Processes;

namespace YapIn.Infrastructure.GitHub;

/// <summary>
/// Tells that something happened on GitHub from two feeds asked with their ETag, which GitHub answers with an empty
/// 304 when nothing changed: your own activity (so an approval of yours shows up) and your notifications (what others do to
/// you). Both are only as quick as GitHub publishes them, and GitHub asks to be polled no more often than they say.
/// </summary>
internal sealed partial class GitHubActivity(IProcessRunner processRunner, GitHubViewer gitHubViewer) : IGitHubActivity
{
    private static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(60);

    private string? eventsEntityTag;
    private string? notificationsEntityTag;
    private HashSet<string> notificationKeys = [];

    public async Task<ActivityCheck> CheckForActivityAsync(CancellationToken cancellationToken)
    {
        var viewer = await gitHubViewer.GetAsync(cancellationToken);
        // The newest event is enough to tell that there is a new one.
        var events = await GetIfChangedAsync($"users/{viewer}/events?per_page=1", eventsEntityTag, cancellationToken);
        var notifications = await GetIfChangedAsync("notifications?participating=true&per_page=50", notificationsEntityTag, cancellationToken);

        // The first answer from a feed is only the baseline: what it holds then is not new.
        var isFirstEvents = eventsEntityTag is null;
        var isFirstNotifications = notificationsEntityTag is null;
        eventsEntityTag = events.EntityTag ?? eventsEntityTag;
        notificationsEntityTag = notifications.EntityTag ?? notificationsEntityTag;

        var notified = notifications.Body is null ? [] : NewlyNotifiedPullRequests(notifications.Body, isFirstNotifications);
        var hasChanged = (events.Body is not null && isFirstEvents == false) || (notifications.Body is not null && isFirstNotifications == false);
        var pollInterval = new[] { MinimumPollInterval, events.PollInterval, notifications.PollInterval }.Max();
        return new ActivityCheck(hasChanged, notified, pollInterval);
    }

    // Threads leave the list once read, so remembering only the current ones keeps this from growing.
    private List<PullRequestUrl> NewlyNotifiedPullRequests(string body, bool isBaseline)
    {
        var threads = JsonSerializer.Deserialize(body, InfrastructureJsonContext.Default.ListNotificationNode) ?? [];
        var previousKeys = notificationKeys;
        notificationKeys = threads.Select(ThreadKey).ToHashSet();
        if (isBaseline) return [];

        var notified = new List<PullRequestUrl>();
        foreach (var thread in threads.Where(thread => previousKeys.Contains(ThreadKey(thread)) == false))
        {
            if (thread.Subject is { Type: "PullRequest", Url: { } apiUrl } && ApiPullRequestUrl().Match(apiUrl) is { Success: true } match &&
                PullRequestUrl.TryParse($"https://github.com/{match.Groups["owner"].Value}/{match.Groups["repository"].Value}/pull/{match.Groups["number"].Value}", out var url))
                notified.Add(url);
        }
        return notified;
    }

    // A thread is new again when something more happens in it.
    private static string ThreadKey(NotificationNode thread) => $"{thread.Id}@{thread.UpdatedAt}";

    /// <summary>gh exits with 1 on a 304, so the answer is read from the status line gh prints with --include.</summary>
    private async Task<Feed> GetIfChangedAsync(string path, string? entityTag, CancellationToken cancellationToken)
    {
        List<string> arguments = ["api", "--include", path];
        if (entityTag is not null) arguments.AddRange(["-H", $"If-None-Match: {entityTag}"]);
        var result = await GhCli.RunAsync(processRunner, arguments, cancellationToken);

        var lines = result.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var statusCode = lines.Count > 0 && StatusLine().Match(lines[0]) is { Success: true } status
            ? int.Parse(status.Groups["code"].Value, CultureInfo.InvariantCulture)
            : 0;
        if (statusCode == 304) return new Feed(entityTag, null, TimeSpan.Zero);
        if (statusCode != 200 || result.Succeeded == false)
            throw new ExternalToolFailedException("gh", $"exited with {result.ExitCode}", result.StandardError);

        var headerEnd = lines.IndexOf("");
        var headers = lines.Skip(1).Take(headerEnd - 1).Select(line => line.Split(':', 2)).Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var pollInterval = headers.TryGetValue("X-Poll-Interval", out var seconds) && int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? TimeSpan.FromSeconds(parsed)
            : TimeSpan.Zero;
        return new Feed(headers.GetValueOrDefault("ETag"), string.Join('\n', lines.Skip(headerEnd + 1)), pollInterval);
    }

    /// <param name="Body">Null when GitHub said nothing changed.</param>
    private sealed record Feed(string? EntityTag, string? Body, TimeSpan PollInterval);

    [GeneratedRegex(@"\AHTTP/\S+ (?<code>[0-9]{3})")]
    private static partial Regex StatusLine();

    [GeneratedRegex(@"\Ahttps://api\.github\.com/repos/(?<owner>[^/]+)/(?<repository>[^/]+)/pulls/(?<number>[0-9]{1,10})\z")]
    private static partial Regex ApiPullRequestUrl();
}
