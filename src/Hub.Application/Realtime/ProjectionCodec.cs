using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hub.Application.Realtime;

public static class ProjectionCodec
{
    public const int MaximumBytes = 2_000_000;
    private const int MaximumOperations = 256;

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Web);
    public static int Bytes<T>(T value) => Encoding.UTF8.GetByteCount(Serialize(value));
    public static string Event(ProjectionFrame frame) => $"id: {frame.Fingerprint}\ndata: {Serialize(frame)}\n\n";
    public static int WireBytes(ProjectionFrame frame) => Encoding.UTF8.GetByteCount(Event(frame));
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static ProjectionSnapshot Snapshot(string scope, object value)
    {
        var data = Canonical(JsonSerializer.SerializeToNode(value, JsonDefaults.Web))!;
        var json = Serialize(data);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new UseCaseException(ErrorKind.Validation, "This view is too large for realtime synchronization. Use the regular API.");
        return new ProjectionSnapshot(scope, Hash("atlas-projection-v1:" + json), data);
    }

    public static ProjectionFrame? Choose(ProjectionSnapshot current, ProjectionSnapshot? previous, string? heldFingerprint)
    {
        if (previous?.Scope == current.Scope && previous.Fingerprint == current.Fingerprint) return null;
        if (heldFingerprint == current.Fingerprint) return new ProjectionFrame("resumed", current.Fingerprint);
        var snapshot = new ProjectionFrame("snapshot", current.Fingerprint, Data: current.Data);
        if (previous?.Scope != current.Scope) return snapshot;
        var operations = new List<ProjectionPatchOperation>();
        Diff(previous.Data, current.Data, "", operations);
        if (operations.Count > MaximumOperations) return snapshot;
        var patch = new ProjectionFrame("patch", current.Fingerprint, previous.Fingerprint, Patch: operations);
        return WireBytes(patch) < WireBytes(snapshot) ? patch : snapshot;
    }

    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject map => new JsonObject(map.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => KeyValuePair.Create(pair.Key, Canonical(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
        _ => node?.DeepClone()
    };

    private static void Diff(JsonNode? before, JsonNode? after, string path, List<ProjectionPatchOperation> changes)
    {
        if (changes.Count > MaximumOperations || JsonNode.DeepEquals(before, after)) return;
        if (before is JsonObject oldMap && after is JsonObject newMap)
        {
            foreach (var key in oldMap.Select(pair => pair.Key).Except(newMap.Select(pair => pair.Key)))
                changes.Add(new("remove", path + "/" + Escape(key)));
            foreach (var pair in newMap)
            {
                if (changes.Count > MaximumOperations) return;
                var child = path + "/" + Escape(pair.Key);
                if (!oldMap.ContainsKey(pair.Key)) changes.Add(new("add", child, pair.Value?.DeepClone()));
                else Diff(oldMap[pair.Key], pair.Value, child, changes);
            }
        }
        else if (before is JsonArray oldArray && after is JsonArray newArray)
        {
            var shared = Math.Min(oldArray.Count, newArray.Count);
            for (var index = 0; index < shared && changes.Count <= MaximumOperations; index++)
                Diff(oldArray[index], newArray[index], path + "/" + index, changes);
            for (var index = oldArray.Count - 1; index >= newArray.Count && changes.Count <= MaximumOperations; index--)
                changes.Add(new("remove", path + "/" + index));
            for (var index = shared; index < newArray.Count && changes.Count <= MaximumOperations; index++)
                changes.Add(new("add", path + "/-", newArray[index]?.DeepClone()));
        }
        else changes.Add(new("replace", path, after?.DeepClone()));
    }

    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
}
