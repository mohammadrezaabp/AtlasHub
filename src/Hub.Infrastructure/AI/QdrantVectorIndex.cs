using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.AI;

public sealed class QdrantVectorIndex(IHttpClientFactory clients, IConfiguration config) : IVectorIndex
{
    public async Task<List<Guid>?> Search(float[] vector, string model, IReadOnlyList<Chunk> authorizedChunks, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(config["Qdrant:Url"])) return null;
        var url = config["Qdrant:Url"] + "/collections/" + EmbeddingSpace.CollectionName(model) + "/points/query";
        using var response = await clients.CreateClient("internal").PostAsJsonAsync(url,
            new { query = vector, limit = 60, with_payload = false, filter = new { must = new[] { new { key = "document_id", match = new { any = authorizedChunks.Select(c => c.DocumentId.ToString()).Distinct().ToArray() } } } } }, ct);
        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        var visible = authorizedChunks.Select(c => c.Id).ToHashSet();
        return json!["result"]!["points"]!.AsArray().Where(n => n?["id"] != null)
            .Select(n => Guid.Parse(n!["id"]!.ToString())).Where(visible.Contains).ToList();
    }

    public async Task Index(Chunk[] chunks, float[][] vectors, string model, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(config["Qdrant:Url"])) return;
        var http = clients.CreateClient("internal");
        var url = config["Qdrant:Url"] + "/collections/" + EmbeddingSpace.CollectionName(model);
        using var check = await http.GetAsync(url, ct);
        if (!check.IsSuccessStatusCode)
        {
            using var create = await http.PutAsJsonAsync(url, new { vectors = new { size = vectors[0].Length, distance = "Cosine" } }, ct);
            if (!create.IsSuccessStatusCode && create.StatusCode != System.Net.HttpStatusCode.Conflict) create.EnsureSuccessStatusCode();
        }
        using var response = await http.PutAsJsonAsync(url + "/points?wait=true", new
        { points = chunks.Select((c, i) => new { id = c.Id, vector = vectors[i], payload = new { document_id = c.DocumentId.ToString(), model } }) }, ct);
        response.EnsureSuccessStatusCode();
    }
}
