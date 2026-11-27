using System.Text.Json;
using System.Text.Json.Serialization;
using YapIn.Infrastructure.GitHub;
using YapIn.Infrastructure.Adapters.Orca;

namespace YapIn.Infrastructure;

// Reflection-based serialisation is off (see Directory.Build.props), so every type read from a program is listed here.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SearchEnvelope))]
[JsonSerializable(typeof(MergeStateEnvelope))]
[JsonSerializable(typeof(PullRequestNodesEnvelope))]
[JsonSerializable(typeof(PullRequestsByAliasEnvelope))]
[JsonSerializable(typeof(List<NotificationNode>))]
[JsonSerializable(typeof(OrcaWorkspaceList))]
internal sealed partial class InfrastructureJsonContext : JsonSerializerContext;
