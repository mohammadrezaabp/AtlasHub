using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Hub.Application.Agents;

public sealed class AgentToolLoop(IModelClient models, IHubToolClient tools, IEventStore events, IHubSessionFactory sessions)
{
    public async Task<bool> Execute(List<object> messages, string prompt, Guid runId, Guid actor, CancellationToken ct)
    {
        var hasToolEvidence = false;
        var openaiTools = KnowledgeTools.Schemas.Select(s =>
        {
            var node = JsonSerializer.SerializeToNode(s)!;
            return (object)new
            {
                type = "function",
                function = new
                {
                    name = node["name"]!.ToString(),
                    description = node["description"]!.ToString(),
                    parameters = node["inputSchema"]
                }
            };
        }).ToArray();
        for (int turn = 0; turn < 2; turn++)
        {
            var response = await models.ToolCompletion(messages.ToArray(), openaiTools, actor.ToString(), ct);
            var calls = response["tool_calls"]?.AsArray();
            if (calls == null || calls.Count == 0)
                break;
            messages.Add(response);
            foreach (var call in calls.Take(4))
            {
                var name = call!["function"]!["name"]!.ToString();
                var arguments = JsonNode.Parse(call["function"]!["arguments"]!.GetValue<string>())!.AsObject();
                JsonNode result;
                if (name == "propose_document_deletion" && !Regex.IsMatch(prompt, @"\b(delete|remove)\b", RegexOptions.IgnoreCase))
                    result = new JsonObject
                    {
                        ["error"] = "An explicit deletion request is required."
                    };
                else if (name == "propose_document_deletion" && !await NamedInPrompt(arguments, prompt, ct))
                    result = new JsonObject
                    {
                        ["error"] = "The user's request must name this document by its title or file name."
                    };
                else
                {
                    await events.Append(runId, new { type = "tool_call", name }, ct);
                    result = await tools.Call(name, arguments, actor, ct);
                    hasToolEvidence = true;
                    if (result is JsonObject approval && approval["approvalId"] != null)
                        await events.Append(runId, new { type = "approval_pending", approvalId = approval["approvalId"]!.ToString() }, ct);
                }

                messages.Add(new { role = "tool", tool_call_id = call["id"]!.ToString(), content = result.ToJsonString() });
                await events.Append(runId, new { type = "tool_result", name, status = result is JsonObject resultObject && resultObject["error"] != null ? "denied" : "completed" }, ct);
            }
        }
        return hasToolEvidence;
    }

    // Binds a model-chosen deletion target to the user's own words, so retrieved text cannot redirect it to another document.
    private async Task<bool> NamedInPrompt(JsonObject arguments, string prompt, CancellationToken ct)
    {
        if (!Guid.TryParse(arguments["documentId"]?.ToString(), out var id))
            return false;
        await using var db = await sessions.CreateAsync(ct);
        var document = await db.FirstOrDefaultAsync(db.Documents.Where(d => d.Id == id), ct);
        return document != null && new[] { document.Title, document.FileName, Path.GetFileNameWithoutExtension(document.FileName) }
            .Any(name => name.Trim().Length >= 3 && prompt.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
