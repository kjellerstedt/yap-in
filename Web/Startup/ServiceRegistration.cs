using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YapIn.Domain.Interfaces;
using YapIn.Domain.Services;
using YapIn.Infrastructure.Adapters;
using YapIn.Infrastructure.Checkouts;
using YapIn.Infrastructure.GitHub;
using YapIn.Infrastructure.Processes;

namespace YapIn.Web.Startup;

/// <summary>The composition root: what each interface is, in one place.</summary>
internal static class ServiceRegistration
{
    /// <summary>Everything except the tools setup chooses. Reads <c>Reviews:ReposDirectory</c> (default <c>~/repos</c>) and <c>Reviews:Agent</c> (default <c>claude</c>).</summary>
    public static IServiceCollection AddYapInServices(this IServiceCollection services, IConfiguration configuration)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        services.AddSingleton(new LocalCheckoutOptions(configuration["Reviews:ReposDirectory"] ?? Path.Combine(homeDirectory, "repos")));
        services.AddSingleton(configuration["Reviews:Agent"] is { } agent ? new AgentCommand(agent) : AgentCommand.Default);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<IPullRequestSearch, GitHubPullRequestSearch>();
        services.AddSingleton<ILocalCheckouts, LocalCheckouts>();
        services.AddSingleton<IInboxService, InboxService>();
        return services;
    }

    /// <summary>The review tool setup chose and, when it starts agent sessions, the session host and the review service.</summary>
    public static IServiceCollection AddChosenTools(this IServiceCollection services, ToolChoices choices)
    {
        services.AddSingleton(choices);
        ToolOptions.Find(ToolOptions.ReviewTools, choices.ReviewTool, "review tool").Register(services);
        if (choices.StartsAgentSessions == false) return services;

        ToolOptions.Find(ToolOptions.SessionHosts, choices.SessionHost!, "session host").Register(services);
        services.AddSingleton<IReviewService, ReviewService>();
        return services;
    }
}
