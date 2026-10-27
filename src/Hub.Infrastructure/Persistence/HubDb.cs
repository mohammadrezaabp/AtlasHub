using Hub.Application.Realtime;
using Microsoft.EntityFrameworkCore;
namespace Hub.Infrastructure.Persistence;

public class HubDb(DbContextOptions<HubDb> options) : DbContext(options)
{
    public DbSet<HubUser> Users => Set<HubUser>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<VoiceAsset> VoiceAssets => Set<VoiceAsset>();
    public DbSet<AgentRun> Runs => Set<AgentRun>();
    public DbSet<RunEvent> Events => Set<RunEvent>();
    public DbSet<ModelUsage> Usages => Set<ModelUsage>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<RealtimeOutboxEntry> RealtimeOutbox => Set<RealtimeOutboxEntry>();

    public void QueueRealtimeChange(string topic)
    {
        if (!ChangeTracker.Entries<RealtimeOutboxEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Topic == topic))
            RealtimeOutbox.Add(new RealtimeOutboxEntry { Topic = topic });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StageRealtimeChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StageRealtimeChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void StageRealtimeChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            switch (entry.Entity)
            {
                case Document: QueueRealtimeChange(RealtimeTopics.Documents); break;
                case Conversation conversation: QueueRealtimeChange(RealtimeTopics.Conversations(conversation.UserId)); break;
                case RunEvent item: QueueRealtimeChange(RealtimeTopics.Run(item.RunId)); break;
                case HubUser user when entry.State == EntityState.Modified &&
                    (entry.Property(nameof(HubUser.SessionVersion)).IsModified || entry.Property(nameof(HubUser.Enabled)).IsModified ||
                     entry.Property(nameof(HubUser.Role)).IsModified || entry.Property(nameof(HubUser.Department)).IsModified):
                    QueueRealtimeChange(RealtimeTopics.Session(user.Id)); break;
                case Setting setting when setting.Key.StartsWith("chat.pin:"):
                    var parts = setting.Key.Split(':');
                    if (parts.Length > 1 && Guid.TryParse(parts[1], out var owner)) QueueRealtimeChange(RealtimeTopics.Conversations(owner));
                    break;
                case ModelUsage usage when Guid.TryParse(usage.Actor, out var actor): QueueRealtimeChange(RealtimeTopics.Usage(actor)); break;
            }
        }
    }
    protected override void OnModelCreating(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(HubDb).Assembly);
}
