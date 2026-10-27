using System.Text.Json.Nodes;

namespace Hub.Application.Realtime;

public sealed record ProjectionPatchOperation(string Op, string Path, JsonNode? Value = null);
