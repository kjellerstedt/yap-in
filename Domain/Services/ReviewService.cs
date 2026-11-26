using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Domain.Services;

/// <summary>
/// Starts the code review tool first, so the page can link straight to its review page; the agent session it then
/// starts is prompted to attach to that review.
///
/// Review tools find their review from the directory the agent runs in, and from a shared checkout the newest review
/// wins. So each session runs in a directory of its own: a workspace its host makes per pull request, or in a terminal
/// the checkout the review tool made for it.
/// </summary>
internal sealed class ReviewService(
    IAgentSessionHost sessionHost,
    ICodeReviewTool codeReviewTool,
    ILocalCheckouts localCheckouts) : IReviewService
{
    // Enough for a review tool's question (e.g. diffo on your own pull request); the page shows it in a dialog.
    private const int MaxToolOutputShown = 2000;

    public async Task<ReviewStarted> StartAsync(PullRequestUrl pullRequestUrl, CancellationToken cancellationToken)
    {
        var localClone = localCheckouts.FindClone(pullRequestUrl.Repository);

        var workspace = await sessionHost.PrepareWorkspaceAsync(pullRequestUrl, localClone, cancellationToken);
        if (workspace is null)
        {
            if (localClone is null) throw new NoLocalCloneException(pullRequestUrl.Repository);
            throw new NoSessionHostException(pullRequestUrl.Repository, sessionHost.Name);
        }

        var review = await codeReviewTool.StartAsync(pullRequestUrl, workspace.Directory, cancellationToken);
        // A plain directory moves into the review tool's own checkout; a host-managed workspace stays where it is.
        var sessionWorkspace = workspace.HostWorkspaceId is null && review.CheckoutPath is { } checkoutPath
            ? workspace with { Directory = checkoutPath }
            : workspace;
        await sessionHost.LaunchAgentAsync(sessionWorkspace, codeReviewTool.AgentPrompt(pullRequestUrl), cancellationToken);
        return new ReviewStarted(
            review.ReviewPageUrl,
            sessionHost.Name,
            review.ReviewPageUrl is null ? Truncate(review.Output, MaxToolOutputShown) : null);
    }

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength] + "…";
}
