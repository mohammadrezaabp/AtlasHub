using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence.Repositories;

public sealed class DocumentIndexStore(IDbContextFactory<HubDb> factory) : IDocumentIndexStore
{
    public async Task<Document?> ClaimNext(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var candidate = await db.Documents.AsNoTracking().Where(d => d.Status == DocumentStatus.Queued || (d.Status == DocumentStatus.Indexing && d.LeaseUntil < now))
            .OrderBy(d => d.CreatedAt).FirstOrDefaultAsync(ct);
        if (candidate == null) return null;
        candidate.BeginIndexing(now);
        var claimed = await db.Documents.Where(d => d.Id == candidate.Id && (d.Status == DocumentStatus.Queued || (d.Status == DocumentStatus.Indexing && d.LeaseUntil < now)))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, candidate.Status).SetProperty(d => d.LeaseUntil, candidate.LeaseUntil)
                .SetProperty(d => d.Attempts, candidate.Attempts), ct);
        return claimed == 0 ? null : await db.Documents.AsNoTracking().SingleAsync(d => d.Id == candidate.Id, ct);
    }

    public async Task Complete(Guid id, IReadOnlyCollection<Chunk> chunks, string? embeddingError, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var document = await db.Documents.SingleAsync(d => d.Id == id, ct);
        document.CompleteIndexing(chunks.Count, embeddingError);
        await db.Chunks.Where(c => c.DocumentId == id).ExecuteDeleteAsync(ct);
        db.Chunks.AddRange(chunks);
        db.Audit.Add(new AuditEntry { Action = "DocumentIndexed", Detail = $"{id}: {chunks.Count} chunks, {document.Status}" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task Fail(Guid id, string error, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Documents.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DocumentStatus.Failed)
            .SetProperty(d => d.Error, error).SetProperty(d => d.LeaseUntil, (DateTime?)null), ct);
    }
}
