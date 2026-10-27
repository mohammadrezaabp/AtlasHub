using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Hub.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
namespace Hub.Infrastructure.Tools;

public sealed class HubToolClient(IHttpClientFactory clients, IConfiguration config) : IHubToolClient
{
    public async Task<JsonNode> Call(string name, JsonObject arguments, Guid actor, CancellationToken ct)
    {
        using var req = InternalRequest.Create(HttpMethod.Post, (config["Tools:Url"] ?? "http://localhost:5082") + "/mcp", config, actor.ToString());
        req.Headers.Add("Accept", "application/json, text/event-stream");
        req.Headers.Add("MCP-Protocol-Version", "2025-03-26");
        req.Content = JsonContent.Create(new { jsonrpc = "2.0", id = Guid.NewGuid().ToString(), method = "tools/call", @params = new { name, arguments } });
        using var response = await clients.CreateClient("internal").SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        if (node["error"] != null) throw new InvalidOperationException("Tool request was rejected.");
        var text = node["result"]!["content"]![0]!["text"]!.GetValue<string>();
        return JsonNode.Parse(text)!;
    }
}
