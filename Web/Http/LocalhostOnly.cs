namespace YapIn.Web.Http;

/// <summary>
/// The server is bound to 127.0.0.1, but a web page can still reach it: by posting to it (cross-site request) or by
/// pointing its own domain at 127.0.0.1 (DNS rebinding), which makes the browser send that domain as Host and Origin.
/// So every request must name localhost as its Host, and a request that changes anything must also come from this
/// page's own origin.
/// </summary>
public sealed class LocalhostOnly(RequestDelegate next)
{
    private static readonly HashSet<string> LocalHostNames = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1" };

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host;
        if (host.HasValue == false || LocalHostNames.Contains(host.Host) == false)
        {
            context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) == false && HttpMethods.IsHead(context.Request.Method) == false &&
            context.Request.Headers.Origin.ToString() != $"{context.Request.Scheme}://{host}")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
