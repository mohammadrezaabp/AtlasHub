using Hub.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace Hub.Infrastructure.Monitoring;

public sealed class SystemMonitor(IDbContextFactory<HubDb> factory, RedisStore redis, IConfiguration config, IHttpClientFactory clients, IFileStore files) : ISystemMonitor
{
    public async Task<SystemStatus> Status(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var services = new List<ServiceHealth>();
        try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); services.Add(new ServiceHealth("MinIO storage", await files.Healthy(timeout.Token) ? "healthy" : "unavailable", files.IsMinio ? "Private object storage" : "Local development storage")); }
        catch { services.Add(new ServiceHealth("MinIO storage", "unavailable", "Storage did not respond")); }
        services.Add(new ServiceHealth("PostgreSQL database", await db.Database.CanConnectAsync() ? "healthy" : "unavailable", config["Database:Provider"] ?? "SQLite"));
        var cache = await redis.GetAsync();
        services.Add(new ServiceHealth("Redis streams", cache != null ? "healthy" : "degraded", cache != null ? "Stream mirror and cache" : "SQL event replay active"));
        foreach (var (name, url) in new[] { ("Model gateway", (config["Gateway:Url"] ?? "http://localhost:5081") + "/health"),
            ("MCP tools", (config["Tools:Url"] ?? "http://localhost:5082") + "/health"), ("Qdrant", (config["Qdrant:Url"] ?? "http://localhost:6333") + "/healthz") })
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); using var r = await clients.CreateClient("internal").GetAsync(url, timeout.Token);
                services.Add(new ServiceHealth(name, r.IsSuccessStatusCode ? "healthy" : "unavailable", "Internal service"));
            }
            catch { services.Add(new ServiceHealth(name, "unavailable", "Service did not respond")); }
        }
        var beat = await db.Settings.FindAsync("worker.heartbeat");
        services.Add(new ServiceHealth("Document worker", DateTime.TryParse(beat?.Value, out var date) && DateTime.UtcNow - date.ToUniversalTime() < TimeSpan.FromMinutes(20) ? "healthy" : "unavailable", beat?.Value ?? "No heartbeat"));
        return new SystemStatus(services, "CPU · external inference", "No local GPU runtime configured", "OpenRouter", ".NET 10 / ASP.NET Core / EF Core / MCP / PostgreSQL / Redis / Qdrant / MinIO");
    }
}
