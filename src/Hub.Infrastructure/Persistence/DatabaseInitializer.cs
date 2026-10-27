using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Hub.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<HubDb>>();
        for (var n = 0; ; n++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync();
                // The web process owns schema initialization; other processes only wait for readiness.
                await db.Database.EnsureCreatedAsync();
                // Additive upgrade for databases created before voice persistence was introduced.
                var createVoices = db.Database.IsNpgsql()
                    ? "CREATE TABLE IF NOT EXISTS \"VoiceAssets\" (\"Id\" uuid NOT NULL PRIMARY KEY, \"OwnerId\" uuid NOT NULL, \"ConversationId\" uuid NULL, \"Department\" TEXT NOT NULL, \"Kind\" TEXT NOT NULL, \"Path\" TEXT NOT NULL, \"ContentType\" TEXT NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL)"
                    : "CREATE TABLE IF NOT EXISTS \"VoiceAssets\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"OwnerId\" TEXT NOT NULL, \"ConversationId\" TEXT NULL, \"Department\" TEXT NOT NULL, \"Kind\" TEXT NOT NULL, \"Path\" TEXT NOT NULL, \"ContentType\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL)";
                await db.Database.ExecuteSqlRawAsync(createVoices);
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_VoiceAssets_OwnerId_CreatedAt\" ON \"VoiceAssets\" (\"OwnerId\", \"CreatedAt\")");
                var createOutbox = db.Database.IsNpgsql()
                    ? "CREATE TABLE IF NOT EXISTS \"RealtimeOutbox\" (\"Id\" uuid NOT NULL PRIMARY KEY, \"Topic\" varchar(160) NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL)"
                    : "CREATE TABLE IF NOT EXISTS \"RealtimeOutbox\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"Topic\" TEXT NOT NULL, \"CreatedAt\" TEXT NOT NULL)";
                await db.Database.ExecuteSqlRawAsync(createOutbox);
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_RealtimeOutbox_CreatedAt\" ON \"RealtimeOutbox\" (\"CreatedAt\")");
                return;
            }
            catch when (n < 30) { await Task.Delay(2000); }
        }
    }

}
