using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Hub.Tests.Support;

public sealed class StubModelClient : IModelClient
{
    public bool FailEmbeddings { get; init; }
    public int EmbeddingCalls { get; private set; }
    public string Model(string role) => "test/" + role.ToLowerInvariant();
    public Task<string> Complete(string role, object[] messages, string actor, int maxTokens = 700, CancellationToken ct = default) => Task.FromResult("Visible image evidence");
    public Queue<JsonObject> ToolResponses { get; } = new();
    public Task<JsonObject> ToolCompletion(object[] messages, object[] tools, string actor, CancellationToken ct) =>
        Task.FromResult(ToolResponses.TryDequeue(out var response) ? response : new JsonObject { ["role"] = "assistant", ["content"] = "Ready" });
    public async IAsyncEnumerable<string> Stream(object[] messages, string actor, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        yield return "Grounded answer [1]";
    }
    public Task<float[][]> Embed(string[] texts, string actor = "system", CancellationToken ct = default)
    {
        EmbeddingCalls++;
        if (FailEmbeddings) throw new ModelException("Embedding provider unavailable");
        return Task.FromResult(texts.Select(_ => new float[] { 1, 0 }).ToArray());
    }
}
