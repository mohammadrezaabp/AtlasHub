using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence;

public sealed class EfHubSession(HubDb db) : IHubSession
{
    public IQueryable<HubUser> Users => db.Users;
    public IQueryable<Document> Documents => db.Documents;
    public IQueryable<Chunk> Chunks => db.Chunks.AsNoTracking().Include(c => c.Document);
    public IQueryable<Conversation> Conversations => db.Conversations.AsNoTracking();
    public IQueryable<Message> Messages => db.Messages.AsNoTracking();
    public IQueryable<VoiceAsset> VoiceAssets => db.VoiceAssets.AsNoTracking();
    public IQueryable<AgentRun> Runs => db.Runs.AsNoTracking();
    public IQueryable<RunEvent> Events => db.Events.AsNoTracking();
    public IQueryable<ModelUsage> Usages => db.Usages.AsNoTracking();
    public IQueryable<Approval> Approvals => db.Approvals.AsNoTracking();
    public IQueryable<AuditEntry> Audit => db.Audit.AsNoTracking();
    public IQueryable<Setting> Settings => db.Settings;
    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.ToListAsync(ct);
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.FirstOrDefaultAsync(ct);
    public Task<T> FirstAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.FirstAsync(ct);
    public Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.SingleAsync(ct);
    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.SingleOrDefaultAsync(ct);
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.AnyAsync(ct);
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default) => query.CountAsync(ct);
    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public void AddRange<T>(IEnumerable<T> entities) where T : class => db.Set<T>().AddRange(entities);
    public void Update<T>(T entity) where T : class => db.Set<T>().Update(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public Task<bool> CanConnectAsync(CancellationToken ct = default) => db.Database.CanConnectAsync(ct);
    public ValueTask DisposeAsync() => db.DisposeAsync();
}
