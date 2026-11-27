using System.Diagnostics;

namespace YapIn.Web.Http;

/// <summary>
/// Logs when an /api request arrives and when it finishes, with the status it returned. ASP.NET Core's own request logs
/// are off (Program.cs) because they also cover every static file and add several lines per request.
/// </summary>
public sealed partial class ApiRequestLog(RequestDelegate next, ILogger<ApiRequestLog> logger)
{
    // The status nginx made up for a client that gives up; ASP.NET Core reports the same for an aborted request.
    private const int ClientClosedRequestStatusCode = 499;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") == false)
        {
            await next(context);
            return;
        }

        var method = context.Request.Method;
        var pathAndQuery = $"{context.Request.Path}{context.Request.QueryString}";
        LogStarted(logger, method, pathAndQuery);
        var startTimestamp = Stopwatch.GetTimestamp();
        var isFinished = false;
        try
        {
            await next(context);
            isFinished = true;
        }
        finally
        {
            // A request that throws is logged as an error by ASP.NET Core, so only a normal finish or an abort is logged here.
            if (isFinished || context.RequestAborted.IsCancellationRequested)
            {
                var statusCode = context.RequestAborted.IsCancellationRequested ? ClientClosedRequestStatusCode : context.Response.StatusCode;
                LogFinished(logger, method, pathAndQuery, statusCode, (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "API request: {Method} {PathAndQuery}")]
    private static partial void LogStarted(ILogger logger, string method, string pathAndQuery);

    [LoggerMessage(Level = LogLevel.Information, Message = "API request finished: {Method} {PathAndQuery} returned {StatusCode} in {ElapsedMilliseconds} ms")]
    private static partial void LogFinished(ILogger logger, string method, string pathAndQuery, int statusCode, long elapsedMilliseconds);
}
