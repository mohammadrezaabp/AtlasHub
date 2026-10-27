using Hub.Application.Realtime;

namespace Hub.Application.Abstractions.Realtime;

public interface IProjectionStateStore
{
    Task<ProjectionSnapshot?> Get(string scope, CancellationToken ct);
    Task Set(ProjectionSnapshot snapshot, CancellationToken ct);
}
