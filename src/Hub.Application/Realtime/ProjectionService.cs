using Hub.Application.Abstractions.Realtime;

namespace Hub.Application.Realtime;

public sealed class ProjectionService(IdentityService identity, DocumentService documents, ConversationService conversations,
    WorkspaceService workspace, IProjectionStateStore states, RealtimeMetrics metrics)
{
    public async Task<ProjectionUpdate> Read(Guid userId, string version, string view, string? heldFingerprint,
        ProjectionSnapshot? previous, CancellationToken ct)
    {
        var user = await identity.GetSessionUser(userId, version, ct);
        _ = RealtimeTopics.For(view, user.Id);
        var scope = ProjectionCodec.Hash($"v1:{user.Id}:{user.SessionVersion}:{user.Role}:{user.Department}:{view}");
        object data = view switch
        {
            "documents" => await documents.List(user, ct),
            "conversations" => await conversations.List(user.Id, ct),
            "overview" => await workspace.Overview(user, ct),
            _ => throw new UseCaseException(ErrorKind.Validation, "Unknown realtime view.")
        };
        var current = ProjectionCodec.Snapshot(scope, data);
        var initial = previous == null;
        if (initial && heldFingerprint != null && heldFingerprint != current.Fingerprint)
        {
            var cached = await states.Get(scope, ct);
            if (cached?.Fingerprint == heldFingerprint && cached.Scope == scope) previous = cached;
        }
        var frame = ProjectionCodec.Choose(current, previous, initial ? heldFingerprint : null);
        await states.Set(current, ct);
        metrics.Record(current, frame);
        return new ProjectionUpdate(current, frame);
    }
}
