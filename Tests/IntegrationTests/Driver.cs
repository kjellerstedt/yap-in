using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using YapIn.Web.Inbox;
using YapIn.Web.Reviews;

namespace YapIn.IntegrationTests;

/// <summary>
/// Calls each endpoint's HandleAsync directly, bypassing the HTTP host (routing, the localhost guard, JSON writing:
/// see Host/). The endpoint class is built from a fresh DI scope per call, as the host would.
/// </summary>
internal sealed class Driver(IServiceProvider services)
{
    public async Task<IResult> ListInboxAsync(string? state = null, string? days = null, bool rebuild = false)
    {
        await using var scope = services.CreateAsyncScope();
        var endpoint = ActivatorUtilities.CreateInstance<ListInboxEndpoint>(scope.ServiceProvider);
        return (await endpoint.HandleAsync(state, days, rebuild, CancellationToken)).Result;
    }

    public async Task<IResult> StartReviewAsync(string? url)
    {
        await using var scope = services.CreateAsyncScope();
        var endpoint = ActivatorUtilities.CreateInstance<StartReviewEndpoint>(scope.ServiceProvider);
        return (await endpoint.HandleAsync(url, CancellationToken)).Result;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}
