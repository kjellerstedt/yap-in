using System.Diagnostics.CodeAnalysis;

namespace YapIn.Domain.Models;

/// <summary>Which pull requests the inbox lists: open ones (plus recently merged), merged, or closed.</summary>
public enum InboxState
{
    Open,
    Merged,
    Closed,
}

public static class InboxStates
{
    public static bool TryParse(string? raw, [NotNullWhen(true)] out InboxState? inboxState)
    {
        inboxState = raw switch
        {
            "open" => InboxState.Open,
            "merged" => InboxState.Merged,
            "closed" => InboxState.Closed,
            _ => null,
        };
        return inboxState is not null;
    }
}
