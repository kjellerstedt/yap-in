using Microsoft.AspNetCore.Http.HttpResults;
using YapIn.Api.Responses;
using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Web.Reviews;

/// <summary>POST /api/reviews?url=&lt;pull request&gt;: starts a code review with an agent session attached.</summary>
public sealed partial class StartReviewEndpoint(IReviewService reviewService, ILogger<StartReviewEndpoint> logger)
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/reviews", (string? url, StartReviewEndpoint endpoint, CancellationToken cancellationToken) =>
            endpoint.HandleAsync(url, cancellationToken));

    internal async Task<Results<Ok<StartReviewResponse>, BadRequest<ErrorResponse>, NotFound<ErrorResponse>, JsonHttpResult<ErrorResponse>>> HandleAsync(
        string? url, CancellationToken cancellationToken)
    {
        // Checked before anything runs: the address becomes a program argument and part of Claude's prompt.
        if (PullRequestUrl.TryParse(url, out var pullRequestUrl) == false)
            return TypedResults.BadRequest(new ErrorResponse("Not a GitHub pull request URL"));
        try
        {
            var started = await reviewService.StartAsync(pullRequestUrl, cancellationToken);
            return TypedResults.Ok(new StartReviewResponse(started.ReviewPageUrl?.Value, started.SessionHost, started.ToolOutput));
        }
        catch (Exception exception) when (exception is NoLocalCloneException or NoSessionHostException)
        {
            return TypedResults.NotFound(new ErrorResponse(exception.Message));
        }
        catch (ExternalToolFailedException exception)
        {
            LogReviewFailed(logger, pullRequestUrl.Value, exception.Message, exception.ToolOutput);
            return TypedResults.Json(new ErrorResponse("Could not start the review. See the server log."), statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Review of {PullRequestUrl} failed to start: {Reason}. It said: {ToolOutput}")]
    private static partial void LogReviewFailed(ILogger logger, string pullRequestUrl, string reason, string? toolOutput);
}
