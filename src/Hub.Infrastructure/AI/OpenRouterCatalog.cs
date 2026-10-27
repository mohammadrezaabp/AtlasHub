using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.AI;

public sealed class OpenRouterCatalog(IConfiguration config, IHttpClientFactory clients) : IModelProviderCatalog, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private JsonArray? catalog;
    private DateTime refreshedAt;

    public async Task<JsonArray> List(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (catalog != null && DateTime.UtcNow - refreshedAt < TimeSpan.FromHours(1)) return (JsonArray)catalog.DeepClone();
            var http = clients.CreateClient("external");
            var chat = JsonNode.Parse(await http.GetStringAsync("https://openrouter.ai/api/v1/models", ct))!["data"]!.AsArray();
            var embeddings = JsonNode.Parse(await http.GetStringAsync("https://openrouter.ai/api/v1/embeddings/models", ct))!["data"]!.AsArray();
            var allowed = new[] { "Chat", "Fast", "Rerank", "Vision", "Embedding", "Fallback" }
                .Select(role => config[$"Models:{role}"]).Where(model => !string.IsNullOrEmpty(model)).ToHashSet();
            catalog = new JsonArray(chat.Concat(embeddings).Where(m => allowed.Contains(m?["id"]?.GetValue<string>())).Select(m => m!.DeepClone()).ToArray());
            refreshedAt = DateTime.UtcNow;
            return (JsonArray)catalog.DeepClone();
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}
