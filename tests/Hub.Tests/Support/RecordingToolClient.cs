using System.Text.Json.Nodes;
using Hub.Application.Abstractions.Tools;

namespace Hub.Tests.Support;

public sealed class RecordingToolClient : IHubToolClient
{
    public List<(string Name, JsonObject Arguments)> Calls { get; } = [];
    public Task<JsonNode> Call(string name, JsonObject arguments, Guid actor, CancellationToken ct)
    {
        Calls.Add((name, arguments));
        return Task.FromResult<JsonNode>(new JsonObject { ["approvalId"] = Guid.NewGuid().ToString() });
    }
}
