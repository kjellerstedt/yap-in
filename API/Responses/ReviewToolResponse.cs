namespace YapIn.Api.Responses;

/// <param name="ReviewTool">What the Review button does: "github" links to the pull request's changes, any other tool starts a review.</param>
public sealed record ReviewToolResponse(string ReviewTool);
