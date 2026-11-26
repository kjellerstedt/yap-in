using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using YapIn.Infrastructure.Processes;
using YapIn.Tests.Shared;

namespace YapIn.IntegrationTests.Host;

/// <summary>
/// The whole HTTP host (Program.cs: routing, the localhost guard, JSON writing) with the programs faked and the
/// background refresh off. For what the driver bypasses; endpoint behaviour is tested through the driver.
/// </summary>
/// <param name="reviewTool">The review tool setup would have saved; "github" leaves out the review endpoint.</param>
internal sealed class HostFixture(string reviewTool = "diffo") : WebApplicationFactory<Program>
{
    public FakeProcessRunner Programs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Chosen here, as setup would save them, so the server never asks.
        builder.UseSetting("Reviews:SessionHost", "terminal");
        builder.UseSetting("Reviews:ReviewTool", reviewTool);
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IProcessRunner>(Programs));
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>A client that talks to the server as this page does: Host and Origin are localhost.</summary>
    public HttpClient CreatePageClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        return client;
    }
}
