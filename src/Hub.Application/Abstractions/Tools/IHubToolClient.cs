using System.Text.Json.Nodes;

namespace Hub.Application.Abstractions.Tools;

public interface IHubToolClient
{
    Task<JsonNode> Call(string name, JsonObject arguments, Guid actor, CancellationToken ct);
}
