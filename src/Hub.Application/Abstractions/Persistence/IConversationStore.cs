namespace Hub.Application.Abstractions.Persistence;

public interface IConversationStore
{
    Task<bool> Rename(Guid id, Guid owner, string title, CancellationToken ct);
    Task<bool> SetPinned(Guid id, Guid owner, bool pinned, CancellationToken ct);
    Task<bool> Delete(Guid id, Guid owner, CancellationToken ct);
    Task<bool> StartRun(Conversation conversation, AgentRun run, bool isNew, CancellationToken ct);
    Task<bool> RequestStop(Guid runId, Guid owner, CancellationToken ct);
}
