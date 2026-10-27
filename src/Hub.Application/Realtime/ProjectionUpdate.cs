namespace Hub.Application.Realtime;

public sealed record ProjectionUpdate(ProjectionSnapshot Snapshot, ProjectionFrame? Frame);
