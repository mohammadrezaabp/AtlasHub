namespace Hub.Application.Monitoring;

public sealed class WorkspaceService(IHubSessionFactory sessions)
{
    public async Task<WorkspaceOverview> Overview(HubUser user, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var documents = db.Documents.VisibleTo(user);
        var usage = await db.ListAsync(db.Usages.Where(u => u.Actor == user.Id.ToString()), ct);
        var recent = await db.ListAsync(documents.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id).Select(d => new DocumentSummary(
            d.Id, d.Title, d.FileName, d.Status, d.Error, d.Visibility, d.Department, d.ChunkCount, d.CreatedAt, user.Role == UserRoles.Administrator || d.OwnerId == user.Id)).Take(5), ct);
        return new WorkspaceOverview(await db.CountAsync(documents, ct), await db.CountAsync(documents.Where(d => d.Status == DocumentStatus.Ready || d.Status == DocumentStatus.LexicalOnly), ct),
            await db.CountAsync(db.Chunks.VisibleTo(user), ct), await db.CountAsync(db.Conversations.Where(c => c.UserId == user.Id), ct),
            usage.Sum(u => u.CostUsd), usage.Sum(u => (long)u.InputTokens + u.OutputTokens), recent);
    }

    public async Task<IReadOnlyList<ModelUsage>> Usage(HubUser actor, CancellationToken ct)
    {
        actor.RequireAdministrator();
        await using var db = await sessions.CreateAsync(ct);
        return await db.ListAsync(db.Usages.OrderByDescending(u => u.Id).Take(300), ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> Audit(HubUser actor, CancellationToken ct)
    {
        actor.RequireAdministrator();
        await using var db = await sessions.CreateAsync(ct);
        return await db.ListAsync(db.Audit.OrderByDescending(a => a.Id).Take(200), ct);
    }
}
