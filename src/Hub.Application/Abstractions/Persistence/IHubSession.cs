namespace Hub.Application.Abstractions.Persistence;

/// <summary>A short-lived unit of work. Queries are executed by the persistence adapter.</summary>
public interface IHubSession : IAsyncDisposable
{
    IQueryable<HubUser> Users { get; }
    IQueryable<Document> Documents { get; }
    IQueryable<Chunk> Chunks { get; }
    IQueryable<Conversation> Conversations { get; }
    IQueryable<Message> Messages { get; }
    IQueryable<VoiceAsset> VoiceAssets { get; }
    IQueryable<AgentRun> Runs { get; }
    IQueryable<RunEvent> Events { get; }
    IQueryable<ModelUsage> Usages { get; }
    IQueryable<Approval> Approvals { get; }
    IQueryable<AuditEntry> Audit { get; }
    IQueryable<Setting> Settings { get; }
    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T> FirstAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    void Add<T>(T entity) where T : class;
    void AddRange<T>(IEnumerable<T> entities) where T : class;
    void Update<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<bool> CanConnectAsync(CancellationToken ct = default);
}
