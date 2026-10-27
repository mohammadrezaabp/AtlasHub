using System.Text.Json.Nodes;
using Hub.Application.Realtime;
using Xunit;

namespace Hub.Tests.Application;

public sealed class ProjectionCodecTests
{
    [Fact]
    public void Fingerprints_are_stable_for_equivalent_object_order_but_preserve_array_order()
    {
        var first = ProjectionCodec.Snapshot("scope", JsonNode.Parse("{\"b\":2,\"a\":[1,2]}")!);
        var reordered = ProjectionCodec.Snapshot("scope", JsonNode.Parse("{\"a\":[1,2],\"b\":2}")!);
        var changed = ProjectionCodec.Snapshot("scope", JsonNode.Parse("{\"a\":[2,1],\"b\":2}")!);
        Assert.Equal(first.Fingerprint, reordered.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Null(ProjectionCodec.Choose(reordered, first, null));
    }

    [Fact]
    public void Large_views_send_a_small_field_patch_and_escape_json_pointer_paths()
    {
        var original = new JsonObject { ["padding"] = new string('x', 2000), ["a/b~c"] = 1 };
        var changed = original.DeepClone();
        changed["a/b~c"] = 2;
        var before = ProjectionCodec.Snapshot("owner", original);
        var after = ProjectionCodec.Snapshot("owner", changed);
        var frame = Assert.IsType<ProjectionFrame>(ProjectionCodec.Choose(after, before, null));
        Assert.Equal("patch", frame.Type);
        Assert.Equal(before.Fingerprint, frame.BaseFingerprint);
        Assert.Equal("/a~1b~0c", Assert.Single(frame.Patch!).Path);
        Assert.True(ProjectionCodec.WireBytes(frame) < ProjectionCodec.WireBytes(new("snapshot", after.Fingerprint, Data: after.Data)));
    }

    [Fact]
    public void Tiny_views_scope_changes_and_large_diffs_use_full_snapshots()
    {
        var before = ProjectionCodec.Snapshot("owner-a", new[] { 1 });
        var after = ProjectionCodec.Snapshot("owner-a", new[] { 2 });
        Assert.Equal("snapshot", ProjectionCodec.Choose(after, before, null)!.Type);
        Assert.Equal("snapshot", ProjectionCodec.Choose(ProjectionCodec.Snapshot("owner-b", new[] { 2 }), before, null)!.Type);
        var largeBefore = ProjectionCodec.Snapshot("scope", Enumerable.Range(0, 400).ToArray());
        var largeAfter = ProjectionCodec.Snapshot("scope", Enumerable.Range(1000, 400).ToArray());
        Assert.Equal("snapshot", ProjectionCodec.Choose(largeAfter, largeBefore, null)!.Type);
    }

    [Fact]
    public void Matching_fingerprint_resumes_without_a_snapshot_and_metrics_count_wire_bytes()
    {
        var snapshot = ProjectionCodec.Snapshot("scope", new { text = "Private state" });
        var resumed = ProjectionCodec.Choose(snapshot, null, snapshot.Fingerprint)!;
        Assert.Equal("resumed", resumed.Type);
        Assert.Null(resumed.Data);
        Assert.Null(resumed.Patch);
        var metrics = new RealtimeMetrics();
        metrics.Record(snapshot, resumed);
        metrics.Record(snapshot, null);
        Assert.Contains("atlas_realtime_suppressed_total 1", metrics.Collect());
        Assert.Contains($"atlas_realtime_frame_bytes_total {ProjectionCodec.WireBytes(resumed)}", metrics.Collect());
    }
}
