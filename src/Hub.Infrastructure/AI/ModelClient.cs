using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.AI;

public sealed class ModelClient(IHttpClientFactory clients, IConfiguration config) : IModelClient
{
    public string Model(string role) => config[$"Models:{role}"] ?? role switch
    {
        "Embedding" => "openai/text-embedding-3-small",
        "Fast" => "openai/gpt-4.1-nano",
        _ => "google/gemini-2.5-flash-lite"
    };
    private HttpRequestMessage Request(string endpoint, string role, string actor, object body)
    {
        var r = InternalRequest.Create(HttpMethod.Post,
            (config["Gateway:Url"] ?? "http://localhost:5081") + "/v1/" + endpoint, config, actor);
        r.Headers.Add("X-Hub-Role", role);
        r.Content = JsonContent.Create(body, options: JsonDefaults.Web);
        return r;
    }
    public async Task<string> Complete(string role, object[] messages, string actor, int maxTokens = 700, CancellationToken ct = default)
    {
        using var activity = HubTelemetry.Activity.StartActivity($"model.{role}");
        using var r = Request("chat/completions", role, actor, new
        { model = Model(role), messages, max_tokens = maxTokens, temperature = 0.1, stream = false });
        using var response = await clients.CreateClient("internal").SendAsync(r, ct);
        await Check(response, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
    public async Task<JsonObject> ToolCompletion(object[] messages, object[] tools, string actor, CancellationToken ct)
    {
        using var r = Request("chat/completions", "Chat", actor, new
        { model = Model("Chat"), messages, tools, tool_choice = "auto", max_tokens = 900, temperature = 0.1, stream = false });
        using var response = await clients.CreateClient("internal").SendAsync(r, ct);
        await Check(response, ct);
        var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        return doc["choices"]![0]!["message"]!.AsObject();
    }
    public async IAsyncEnumerable<string> Stream(object[] messages, string actor,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var r = Request("chat/completions", "Chat", actor, new
        { model = Model("Chat"), messages, max_tokens = 1800, temperature = 0.2, stream = true, stream_options = new { include_usage = true } });
        using var response = await clients.CreateClient("internal").SendAsync(r, HttpCompletionOption.ResponseHeadersRead, ct);
        await Check(response, ct);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") yield break;
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("error", out _)) throw new ModelException("The model interrupted its response. Please retry.");
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                yield return content.GetString()!;
        }
    }
    public async Task<float[][]> Embed(string[] texts, string actor = "system", CancellationToken ct = default)
    {
        using var r = Request("embeddings", "Embedding", actor, new { model = Model("Embedding"), input = texts, encoding_format = "float" });
        using var response = await clients.CreateClient("internal").SendAsync(r, ct);
        await Check(response, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").EnumerateArray().OrderBy(x => x.GetProperty("index").GetInt32())
            .Select(x => x.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray();
    }
    private static async Task Check(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        string? message = null;
        try { message = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>(); } catch (JsonException) { }
        throw new ModelException(message ?? $"Model service unavailable ({(int)response.StatusCode}).");
    }
}
