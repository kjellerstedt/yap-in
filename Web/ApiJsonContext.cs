using System.Text.Json;
using System.Text.Json.Serialization;
using YapIn.Api.Responses;

namespace YapIn.Web;

// Reflection-based serialisation is off (see Directory.Build.props), so every response type is listed here.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(InboxResponse))]
[JsonSerializable(typeof(StartReviewResponse))]
[JsonSerializable(typeof(ReviewToolResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
