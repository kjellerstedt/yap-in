using YapIn.Web;
using YapIn.Web.Http;
using YapIn.Web.Inbox;
using YapIn.Web.Reviews;
using YapIn.Web.Startup;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:4477");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

ToolChoices toolChoices;
try
{
    toolChoices = ToolChoices.Resolve(
        builder.Configuration, UserSettingsFile.Default, args.Contains("--setup"), new SetupPrompt(Console.In, Console.Out),
        terminalAvailable: Console.IsInputRedirected == false);
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

builder.Services
    .AddYapInServices(builder.Configuration)
    .AddChosenTools(toolChoices)
    .AddTransient<ListInboxEndpoint>()
    .AddHostedService<InboxRefreshService>();
if (toolChoices.StartsAgentSessions) builder.Services.AddTransient<StartReviewEndpoint>();

var app = builder.Build();
app.UseMiddleware<LocalhostOnly>();
app.UseDefaultFiles();
app.UseStaticFiles();
ListInboxEndpoint.Map(app);
ReviewToolEndpoint.Map(app);
if (toolChoices.StartsAgentSessions) StartReviewEndpoint.Map(app);
app.Run();
return 0;

public partial class Program;
