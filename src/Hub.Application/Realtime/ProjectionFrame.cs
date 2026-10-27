using System.Text.Json.Nodes;

namespace Hub.Application.Realtime;

public sealed record ProjectionFrame(string Type, string Fingerprint, string? BaseFingerprint = null,
    JsonNode? Data = null, IReadOnlyList<ProjectionPatchOperation>? Patch = null, int Schema = 1);
