using Microsoft.AspNetCore.Http.HttpResults;
using YapIn.Api.Responses;
using YapIn.Domain.Exceptions;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Models;
using YapIn.Domain.Models.Primitives;

namespace YapIn.Web.Inbox;

/// <summary>GET /api/prs?state=open|merged|closed&amp;days=1..365[&amp;refresh]: the pull requests that involve you.</summary>
public sealed partial class ListInboxEndpoint(IInboxService inboxService, ILogger<ListInboxEndpoint> logger)
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/prs", (string? state, string? days, string? refresh, ListInboxEndpoint endpoint, CancellationToken cancellationToken) =>
            endpoint.HandleAsync(state, days, rebuild: refresh is not null, cancellationToken));

    // Unknown values fall back to the default inbox rather than failing: they only ever come from this page's own URL.
    internal async Task<Results<Ok<InboxResponse>, JsonHttpResult<ErrorResponse>>> HandleAsync(
        string? state, string? days, bool rebuild, CancellationToken cancellationToken)
    {
        var inboxState = InboxStates.TryParse(state, out var parsedState) ? parsedState.Value : InboxState.Open;
        var lookback = LookbackDays.TryParse(days, out var parsedDays) ? parsedDays : LookbackDays.Default;
        var query = new InboxQuery(inboxState, lookback);
        try
        {
            var inbox = rebuild ? await inboxService.RebuildAsync(query, cancellationToken) : await inboxService.GetAsync(query, cancellationToken);
            return TypedResults.Ok(inbox.ToResponse());
        }
        catch (ExternalToolFailedException exception)
        {
            LogSearchFailed(logger, exception.Message, exception.ToolOutput);
            return TypedResults.Json(new ErrorResponse("Could not load pull requests from GitHub. Is gh signed in? See the server log."),
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Inbox search failed: {Reason}. It said: {ToolOutput}")]
    private static partial void LogSearchFailed(ILogger logger, string reason, string? toolOutput);
}
