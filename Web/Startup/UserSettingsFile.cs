using System.Text.Json;
using System.Text.Json.Nodes;

namespace YapIn.Web.Startup;

/// <summary>
/// What setup saved, in <c>~/.config/yap-in/settings.json</c>: <c>{"Reviews":{"SessionHost":…,"ReviewTool":…}}</c>,
/// without <c>SessionHost</c> when the review tool starts no agent sessions.
/// </summary>
internal sealed class UserSettingsFile(string filePath)
{
    public static UserSettingsFile Default => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "yap-in", "settings.json"));

    public string FilePath { get; } = filePath;

    /// <returns>The saved choices, or <c>null</c> when nothing is saved yet.</returns>
    public ToolChoices? Read()
    {
        if (File.Exists(FilePath) == false) return null;
        try
        {
            var reviews = JsonNode.Parse(File.ReadAllText(FilePath))?["Reviews"];
            return reviews?["ReviewTool"]?.GetValue<string>() is { } reviewTool
                ? new ToolChoices(reviews["SessionHost"]?.GetValue<string>(), reviewTool)
                : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException($"{FilePath} is not a settings file this server can read. Fix it or delete it and run with --setup.", exception);
        }
    }

    public void Save(ToolChoices choices)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var reviews = new JsonObject();
        if (choices.SessionHost is not null) reviews["SessionHost"] = choices.SessionHost;
        reviews["ReviewTool"] = choices.ReviewTool;
        var settings = new JsonObject { ["Reviews"] = reviews };
        File.WriteAllText(FilePath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
