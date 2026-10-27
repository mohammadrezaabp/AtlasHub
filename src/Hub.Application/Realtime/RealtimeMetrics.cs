namespace Hub.Application.Realtime;

public sealed class RealtimeMetrics
{
    private long evaluations, suppressed, snapshots, patches, resumes, bytes, baseline;
    public void Record(ProjectionSnapshot snapshot, ProjectionFrame? frame)
    {
        Interlocked.Increment(ref evaluations);
        Interlocked.Add(ref baseline, ProjectionCodec.WireBytes(new ProjectionFrame("snapshot", snapshot.Fingerprint, Data: snapshot.Data)));
        if (frame == null) { Interlocked.Increment(ref suppressed); return; }
        Interlocked.Add(ref bytes, ProjectionCodec.WireBytes(frame));
        switch (frame.Type)
        {
            case "snapshot": Interlocked.Increment(ref snapshots); break;
            case "patch": Interlocked.Increment(ref patches); break;
            case "resumed": Interlocked.Increment(ref resumes); break;
        }
    }

    public string Collect() => $"# TYPE atlas_realtime_evaluations_total counter\natlas_realtime_evaluations_total {Interlocked.Read(ref evaluations)}\n" +
        $"# TYPE atlas_realtime_suppressed_total counter\natlas_realtime_suppressed_total {Interlocked.Read(ref suppressed)}\n" +
        $"# TYPE atlas_realtime_snapshots_total counter\natlas_realtime_snapshots_total {Interlocked.Read(ref snapshots)}\n" +
        $"# TYPE atlas_realtime_patches_total counter\natlas_realtime_patches_total {Interlocked.Read(ref patches)}\n" +
        $"# TYPE atlas_realtime_resumes_total counter\natlas_realtime_resumes_total {Interlocked.Read(ref resumes)}\n" +
        $"# TYPE atlas_realtime_frame_bytes_total counter\natlas_realtime_frame_bytes_total {Interlocked.Read(ref bytes)}\n" +
        $"# TYPE atlas_realtime_snapshot_baseline_bytes_total counter\natlas_realtime_snapshot_baseline_bytes_total {Interlocked.Read(ref baseline)}\n";
}
