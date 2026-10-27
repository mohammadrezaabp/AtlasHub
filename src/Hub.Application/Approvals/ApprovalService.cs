namespace Hub.Application.Approvals;

public sealed class ApprovalService(IHubSessionFactory sessions, IApprovalStore approvals, IStorageMaintenance storage)
{
    public async Task<IReadOnlyList<ApprovalSummary>> List(HubUser actor, CancellationToken ct)
    {
        actor.RequireAdministrator();
        await using var db = await sessions.CreateAsync(ct);
        return await db.ListAsync(db.Approvals.OrderByDescending(a => a.CreatedAt).Select(a => new ApprovalSummary(a.Id, a.DocumentId, a.Action, a.Status, a.CreatedAt,
            db.Documents.Where(d => d.Id == a.DocumentId).Select(d => d.Title).FirstOrDefault())).Take(100), ct);
    }

    public async Task Resolve(HubUser actor, Guid id, bool approve, CancellationToken ct)
    {
        actor.RequireAdministrator();
        if (!await approvals.Resolve(id, actor, approve, ct)) throw new UseCaseException(ErrorKind.NotFound, "Approval not found.");
        await storage.DrainDeletes(ct);
    }
}
