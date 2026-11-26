using YapIn.Domain.Models.Primitives;
using YapIn.Infrastructure.Adapters.Diffo;

namespace YapIn.ContractTests.Diffo;

/// <summary>Against the real diffo, fetched by npx as the server does. It only asks for diffo's own help.</summary>
public sealed class DiffoContractTests
{
    [Fact]
    public async Task Knows_every_flag_we_send_in_its_own_help()
    {
        // Arrange
        RealTools.SkipUnlessOnPath("npx");
        var pullRequest = PullRequestUrl.TryParse("https://github.com/acme/search/pull/412", out var parsed) ? parsed : throw new InvalidOperationException();
        string[] helpArguments = ["-y", DiffoCodeReviewTool.PinnedPackage, "pr", "--help"];

        // Act and assert
        await RealTools.AssertHelpKnowsFlagsAsync("npx", helpArguments, DiffoCodeReviewTool.StartArguments(pullRequest));
    }
}
