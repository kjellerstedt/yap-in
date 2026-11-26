namespace YapIn.Api.Responses;

/// <param name="ReviewUrl">The local review page, or <c>null</c> when the review tool gave none.</param>
/// <param name="SessionHost">Where the agent session started, e.g. "orca" or "iterm".</param>
/// <param name="ToolOutput">What the review tool said, when it gave no review page (it may be asking a question).</param>
public sealed record StartReviewResponse(string? ReviewUrl, string SessionHost, string? ToolOutput);
