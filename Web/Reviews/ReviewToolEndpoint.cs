using YapIn.Api.Responses;
using YapIn.Web.Startup;

namespace YapIn.Web.Reviews;

/// <summary>GET /api/review-tool: the review tool setup chose, so the page knows what its Review button does.</summary>
internal static class ReviewToolEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/review-tool", (ToolChoices choices) => TypedResults.Ok(new ReviewToolResponse(choices.ReviewTool)));
}
