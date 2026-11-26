using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YapIn.Domain.Interfaces;
using YapIn.Web.Startup;

namespace YapIn.IntegrationTests.Startup;

public sealed class SetupTests : IDisposable
{
    private readonly string settingsDirectory = Directory.CreateTempSubdirectory("yap-in-settings-").FullName;
    private readonly StringWriter terminalOutput = new();

    public void Dispose() => Directory.Delete(settingsDirectory, recursive: true);

    [Fact]
    public void Asks_on_first_start_and_saves_the_answer()
    {
        // Arrange
        var settingsFile = NewSettingsFile();

        // Act
        var choices = Resolve(settingsFile, answers: "2\n3\n");

        // Assert
        Assert.Equal(new ToolChoices("terminal", "diffo"), choices);
        Assert.Equal(choices, settingsFile.Read());
        Assert.Contains("macOS Terminal", terminalOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("npx -y @diffohq/diffo@0.35.0 setup", terminalOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_the_saved_choices_without_asking()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        settingsFile.Save(new ToolChoices("iterm", "diffo"));

        // Act
        var choices = Resolve(settingsFile, answers: "");

        // Assert
        Assert.Equal(new ToolChoices("iterm", "diffo"), choices);
        Assert.Equal("", terminalOutput.ToString());
    }

    [Fact]
    public void Asks_again_when_setup_is_requested_and_a_bare_enter_keeps_the_saved_host()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        settingsFile.Save(new ToolChoices("iterm", "diffo"));

        // Act
        var choices = Resolve(settingsFile, answers: "\n\n", setupRequested: true);

        // Assert
        Assert.Equal(new ToolChoices("iterm", "diffo"), choices);
        Assert.Contains("[iterm]", terminalOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Saves_a_changed_answer_when_setup_is_requested()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        settingsFile.Save(new ToolChoices("iterm", "diffo"));

        // Act
        var choices = Resolve(settingsFile, answers: "\norca\n", setupRequested: true);

        // Assert
        Assert.Equal(new ToolChoices("orca", "diffo"), choices);
        Assert.Equal(choices, settingsFile.Read());
    }

    [Fact]
    public void Asks_again_after_an_answer_that_is_not_an_option()
    {
        // Act
        var choices = Resolve(NewSettingsFile(), answers: "diffo\n9\nbanana\n2\n");

        // Assert
        Assert.Equal("iterm", choices.SessionHost);
    }

    [Fact]
    public void Asks_nothing_more_when_the_review_button_opens_github()
    {
        // Arrange
        var settingsFile = NewSettingsFile();

        // Act
        var choices = Resolve(settingsFile, answers: "1\n");

        // Assert
        Assert.Equal(new ToolChoices(null, "github"), choices);
        Assert.Equal(choices, settingsFile.Read());
        Assert.DoesNotContain("Where should review sessions run?", terminalOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("npx", terminalOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_a_saved_github_choice_without_asking_for_a_session_host()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        settingsFile.Save(new ToolChoices(null, "github"));

        // Act
        var choices = Resolve(settingsFile, answers: "");

        // Assert
        Assert.Equal(new ToolChoices(null, "github"), choices);
        Assert.Equal("", terminalOutput.ToString());
    }

    [Fact]
    public void Lets_a_setting_override_the_saved_choice_without_asking()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        settingsFile.Save(new ToolChoices("iterm", "diffo"));

        // Act
        var choices = Resolve(settingsFile, answers: "", settings: new() { ["Reviews:SessionHost"] = "orca" });

        // Assert
        Assert.Equal(new ToolChoices("orca", "diffo"), choices);
        Assert.Equal(new ToolChoices("iterm", "diffo"), settingsFile.Read());
    }

    [Fact]
    public void Stops_and_says_how_to_set_up_when_nothing_is_saved_and_there_is_no_terminal()
    {
        // Arrange
        var settingsFile = NewSettingsFile();

        // Act
        var resolve = () => Resolve(settingsFile, answers: "", terminalAvailable: false);

        // Assert
        Assert.Contains("--setup", Assert.Throws<InvalidOperationException>(resolve).Message, StringComparison.Ordinal);
        Assert.Null(settingsFile.Read());
    }

    [Fact]
    public void Stops_when_the_terminal_gives_no_answer()
    {
        // Act
        var resolve = () => Resolve(NewSettingsFile(), answers: "");

        // Assert
        Assert.Throws<InvalidOperationException>(resolve);
    }

    [Fact]
    public void Names_the_options_when_a_setting_names_a_tool_that_does_not_exist()
    {
        // Act
        var resolve = () => Resolve(NewSettingsFile(), answers: "", settings: new() { ["Reviews:SessionHost"] = "warp", ["Reviews:ReviewTool"] = "diffo" });

        // Assert
        Assert.Equal("Unknown session host \"warp\". Choose one of: orca, iterm, terminal", Assert.Throws<InvalidOperationException>(resolve).Message);
    }

    [Fact]
    public void Says_which_file_to_fix_when_the_saved_settings_cannot_be_read()
    {
        // Arrange
        var settingsFile = NewSettingsFile();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile.FilePath)!);
        File.WriteAllText(settingsFile.FilePath, "{ not json");

        // Act
        var resolve = () => Resolve(settingsFile, answers: "");

        // Assert
        Assert.Contains(settingsFile.FilePath, Assert.Throws<InvalidOperationException>(resolve).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("orca")]
    [InlineData("iterm")]
    [InlineData("terminal")]
    public void Wires_the_session_host_the_choice_names(string sessionHost)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddYapInServices(new ConfigurationBuilder().Build());
        services.AddChosenTools(new ToolChoices(sessionHost, "diffo"));
        using var provider = services.BuildServiceProvider();

        // Act
        var wired = provider.GetRequiredService<IAgentSessionHost>();

        // Assert
        Assert.Equal(sessionHost, wired.Name);
        Assert.NotNull(provider.GetRequiredService<ICodeReviewTool>());
    }

    [Fact]
    public void Wires_no_review_service_when_the_review_button_opens_github()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddYapInServices(new ConfigurationBuilder().Build());
        services.AddChosenTools(new ToolChoices(null, "github"));

        // Act
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Assert
        Assert.Null(provider.GetService<IReviewService>());
        Assert.Null(provider.GetService<IAgentSessionHost>());
    }

    private UserSettingsFile NewSettingsFile() => new(Path.Combine(settingsDirectory, "yap-in", "settings.json"));

    private ToolChoices Resolve(
        UserSettingsFile settingsFile, string answers, bool setupRequested = false, bool terminalAvailable = true, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        return ToolChoices.Resolve(configuration, settingsFile, setupRequested, new SetupPrompt(new StringReader(answers), terminalOutput), terminalAvailable);
    }
}
