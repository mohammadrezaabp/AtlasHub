using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence.Repositories;

public sealed class ApprovalStore(IDbContextFactory<HubDb> factory) : IApprovalStore
{
    public async Task<bool> Resolve(Guid id, HubUser administrator, bool approve, CancellationToken ct)
    {
        if (administrator.Role != UserRoles.Administrator) throw new UseCaseException(ErrorKind.Forbidden, "Administrator access is required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var approval = await db.Approvals.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (approval == null) return false;
        approval.Resolve(approve);
        if (await db.Approvals.Where(a => a.Id == id && a.Status == ApprovalStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, approval.Status), ct) == 0)
            throw new UseCaseException(ErrorKind.Conflict, "This approval has already been resolved.");
        // ExecuteUpdate has persisted the decision; don't emit a second tracked update.
        db.Entry(approval).State = EntityState.Detached;
        if (approve)
        {
            var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == approval.DocumentId, ct);
            if (document == null) throw new UseCaseException(ErrorKind.NotFound, "Document not found.");
            document.EnsureCanDelete();
            db.Documents.Remove(document);
            db.Settings.Add(new Setting { Key = $"storage.delete:{document.Id}", Value = document.Path });
        }
        db.Audit.Add(new AuditEntry { Actor = administrator.Id.ToString(), Action = approve ? "DocumentDeletionApproved" : "DocumentDeletionRejected", Detail = approval.DocumentId.ToString() });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
