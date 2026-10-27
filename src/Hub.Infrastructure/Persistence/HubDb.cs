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
    protected override void OnModelCreating(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(HubDb).Assembly);
}
