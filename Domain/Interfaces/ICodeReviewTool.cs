using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Interfaces;

/// <summary>
/// A tool that opens a pull request for review in your browser, with an agent attached (diffo today). Chosen in setup;
/// throws ExternalToolFailedException when its program fails.
/// </summary>
public interface ICodeReviewTool
{
    /// <summary>What to ask the agent, so it attaches to the review this tool started.</summary>
    string AgentPrompt(PullRequestUrl pullRequestUrl);

    /// <summary>Starts (or reuses) the review, running the tool in <paramref name="workingDirectory"/>, a checkout of the repo.</summary>
    Task<CodeReviewStarted> StartAsync(PullRequestUrl pullRequestUrl, string workingDirectory, CancellationToken cancellationToken);
}
