namespace Hub.Application.Abstractions.Persistence;

public interface IRunStore
{
    Task<AgentRun?> ClaimNext(string owner, CancellationToken ct);
    Task<IReadOnlyList<AgentRun>> RecoverExpired(CancellationToken ct);
    Task<bool> Heartbeat(Guid id, string owner, CancellationToken ct);
    Task Complete(AgentRun run, string owner, string status, string answer, IReadOnlyList<Citation> sources, CancellationToken ct = default);
}
