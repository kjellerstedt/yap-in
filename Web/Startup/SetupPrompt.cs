namespace YapIn.Web.Startup;

/// <summary>Asks, in the terminal the server was started from, what the Review button does and where its sessions run.</summary>
internal sealed class SetupPrompt(TextReader input, TextWriter output)
{
    /// <param name="currentSessionHost">What is chosen now, offered as the answer to a bare Enter.</param>
    /// <param name="currentReviewTool">The same for the review tool.</param>
    public ToolChoices Ask(string? currentSessionHost, string? currentReviewTool)
    {
        output.WriteLine("yap-in setup. Change it later with: dotnet run --project Web -- --setup");
        output.WriteLine();
        var reviewTool = Choose("What should the Review button do?", ToolOptions.ReviewTools, currentReviewTool);
        var choices = new ToolChoices(SessionHost: null, reviewTool.Name);
        if (choices.StartsAgentSessions)
            choices = choices with { SessionHost = Choose("Where should review sessions run?", ToolOptions.SessionHosts, currentSessionHost).Name };

        output.WriteLine();
        if (reviewTool.SetupNote is not null)
        {
            output.WriteLine(reviewTool.SetupNote);
            output.WriteLine();
        }
        return choices;
    }

    private ToolOption Choose(string question, IReadOnlyList<ToolOption> options, string? currentName)
    {
        if (options.Count == 1)
        {
            output.WriteLine($"{question} {options[0].Label} (the only option)");
            return options[0];
        }

        output.WriteLine(question);
        for (var index = 0; index < options.Count; index++) output.WriteLine($"  {index + 1}) {options[index].Label}");
        var current = options.FirstOrDefault(option => option.Name == currentName);
        while (true)
        {
            output.Write(current is null ? $"Choose 1-{options.Count}: " : $"Choose 1-{options.Count} [{current.Name}]: ");
            var answer = input.ReadLine()?.Trim()
                ?? throw new InvalidOperationException("Setup stopped: no answer was given.");
            if (answer.Length == 0 && current is not null) return current;
            if (int.TryParse(answer, out var number) && number >= 1 && number <= options.Count) return options[number - 1];
            if (options.FirstOrDefault(option => option.Name == answer) is { } named) return named;
        }
    }
}
