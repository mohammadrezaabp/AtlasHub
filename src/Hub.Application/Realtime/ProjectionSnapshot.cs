using System.Text.Json.Nodes;

namespace Hub.Application.Realtime;

public sealed record ProjectionSnapshot(string Scope, string Fingerprint, JsonNode Data);
