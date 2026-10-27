using System.Text.Json.Nodes;

namespace Hub.Application.Abstractions.AI;

public interface IModelClient
{
    string Model(string role);
    Task<string> Complete(string role, object[] messages, string actor, int maxTokens = 700, CancellationToken ct = default);
    Task<JsonObject> ToolCompletion(object[] messages, object[] tools, string actor, CancellationToken ct);
    IAsyncEnumerable<string> Stream(object[] messages, string actor, CancellationToken ct = default);
    Task<float[][]> Embed(string[] texts, string actor = "system", CancellationToken ct = default);
}
