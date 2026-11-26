using System.Text.Json;
using System.Text.RegularExpressions;
using YapIn.Api.Responses;

namespace YapIn.IntegrationTests;

/// <summary>The page's TypeScript type for a pull request must name exactly the fields the server sends.</summary>
public sealed partial class PullRequestResponseShapeTests
{
    [Fact]
    public void The_page_and_the_server_agree_on_every_pull_request_field()
    {
        // Arrange
        var typesFile = Path.Combine(RepositoryRoot(), "frontend", "src", "inbox", "types.ts");

        // Act
        var pageFields = InterfaceFields(File.ReadAllText(typesFile), "PullRequest");
        var serverFields = typeof(PullRequestResponse).GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name));

        // Assert
        Assert.Equal(serverFields.Order(StringComparer.Ordinal), pageFields.Order(StringComparer.Ordinal));
    }

    private static IEnumerable<string> InterfaceFields(string typeScript, string interfaceName)
    {
        var body = Regex.Match(typeScript, $@"export interface {interfaceName} \{{(?<body>.*?)\n\}}", RegexOptions.Singleline).Groups["body"].Value;
        return FieldLine().Matches(body).Select(match => match.Groups["name"].Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && File.Exists(Path.Combine(directory.FullName, "YapIn.slnx")) == false) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("YapIn.slnx not found above the test output");
    }

    [GeneratedRegex(@"^  (?<name>[a-zA-Z]+):", RegexOptions.Multiline)]
    private static partial Regex FieldLine();
}
