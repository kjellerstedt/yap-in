using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YapIn.Infrastructure.Adapters.Diffo;
using YapIn.Infrastructure.Processes;
using YapIn.Tests.Shared;
using YapIn.Web.Inbox;
using YapIn.Web.Reviews;
using YapIn.Web.Startup;

namespace YapIn.IntegrationTests;

/// <summary>
/// The real registrations (Web/Startup), with the session host named in setup, and fakes only where the server meets
/// something it does not own: the programs it runs (gh, npx, orca, osascript), the clock, and a temporary directory
/// standing in for ~/repos and diffo's worktrees. Whether the real programs accept what is sent to them is the contract
/// tests' job. Create one per test so state is never shared.
/// </summary>
internal sealed class Fixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider services;

    /// <param name="sessionHost">The session host setup chose; iTerm unless a test needs another.</param>
    public Fixture(string sessionHost = "iterm")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Reviews:ReposDirectory", ReposDirectory)])
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        serviceCollection.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        serviceCollection.AddYapInServices(configuration);
        serviceCollection.AddChosenTools(new ToolChoices(sessionHost, "diffo"));
        serviceCollection.Replace(ServiceDescriptor.Singleton<IProcessRunner>(Programs));
        serviceCollection.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
        serviceCollection.Replace(ServiceDescriptor.Singleton(new DiffoOptions(DiffoWorktreesDirectory)));
        serviceCollection.AddTransient<ListInboxEndpoint>();
        serviceCollection.AddTransient<StartReviewEndpoint>();

        services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Driver = new Driver(services);
    }

    public FakeProcessRunner Programs { get; } = new();
    public FakeTimeProvider Clock { get; } = new(Now);
    public string ReposDirectory { get; } = Directory.CreateTempSubdirectory("yap-in-repos-").FullName;
    public string DiffoWorktreesDirectory => Path.Combine(ReposDirectory, ".diffo", "worktrees");
    public Driver Driver { get; }

    /// <summary>A clone of <paramref name="repositoryName"/> under the stand-in for ~/repos.</summary>
    public string AddClone(string repositoryName)
    {
        var clonePath = Path.Combine(ReposDirectory, repositoryName);
        Directory.CreateDirectory(Path.Combine(clonePath, ".git"));
        return clonePath;
    }

    /// <summary>A checkout inside diffo's own worktrees, as diffo makes for a pull request.</summary>
    public string AddDiffoCheckout(string repositoryName, int pullRequestNumber) =>
        Directory.CreateDirectory(Path.Combine(DiffoWorktreesDirectory, $"{repositoryName}-ab12", $"pr-{pullRequestNumber}")).FullName;

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        Directory.Delete(ReposDirectory, recursive: true);
    }
}
