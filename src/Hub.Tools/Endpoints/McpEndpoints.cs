using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hub.Tools.Endpoints;

public static class McpEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/mcp", () => Results.StatusCode(405));
        app.MapDelete("/mcp", () => Results.StatusCode(405));
        app.MapPost("/mcp", Handle);
    }

    private static async Task<IResult> Handle(HttpContext ctx, KnowledgeTools tools, IHubSessionFactory factory)
    {
        JsonObject request;
        try
        {
            request = (await JsonNode.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted))!.AsObject();
        }
        catch
        {
            return Results.Json(new { jsonrpc = "2.0", id = (string?)null, error = new { code = -32700, message = "Parse error" } });
        }

        var id = request["id"]?.DeepClone();
        if (request["jsonrpc"]?.ToString() != "2.0")
            return Results.BadRequest();
        if (id == null)
            return Results.Accepted();
        try
        {
            object result;
            switch (request["method"]?.ToString())
            {
                case "initialize":
                    result = new
                    {
                        protocolVersion = "2025-03-26",
                        capabilities = new
                        {
                            tools = new
                            {
                                listChanged = false
                            }
                        },
                        serverInfo = new
                        {
                            name = "atlas-knowledge-tools",
                            version = "1.0.0"
                        }
                    };
                    break;
                case "ping":
                    result = new
                    {
                    };
                    break;
                case "tools/list":
                    result = new
                    {
                        tools = KnowledgeTools.Schemas
                    };
                    break;
                case "tools/call":
                    await using (var db = await factory.CreateAsync(ctx.RequestAborted))
                    {
                        if (!Guid.TryParse(ctx.Request.Headers["X-Hub-Actor"], out var userId))
                            return Results.Unauthorized();
                        var user = await db.FirstOrDefaultAsync(db.Users.Where(u => u.Id == userId && u.Enabled), ctx.RequestAborted);
                        if (user == null)
                            return Results.Unauthorized();
                        var args = request["params"]?["arguments"]?.AsObject() ?? new JsonObject();
                        try
                        {
                            var output = await tools.Execute(request["params"]!["name"]!.GetValue<string>(), args, user, ctx.RequestAborted);
                            result = new
                            {
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = JsonSerializer.Serialize(output, JsonDefaults.Web)
                                    }
                                },
                                isError = false
                            };
                        }
                        catch (Exception e) when (e is ArgumentException or UnauthorizedAccessException)
                        {
                            result = new
                            {
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = JsonSerializer.Serialize(new { error = e.Message })
                                    }
                                },
                                isError = true
                            };
                        }
                    }

                    break;
                default:
                    return Results.Json(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Method not found" } });
            }

            return Results.Json(new { jsonrpc = "2.0", id, result });
        }
        catch
        {
            return Results.Json(new { jsonrpc = "2.0", id, error = new { code = -32603, message = "Tool service unavailable" } });
        }
    }
}
