using System.Text;
using System.Text.Json;
using Hub.Application.Abstractions.Realtime;
using Hub.Application.Realtime;
using Hub.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Hub.Infrastructure.Realtime;

public sealed class ProjectionStateStore(RedisStore redis) : IProjectionStateStore, IDisposable
{
    private readonly MemoryCache memory = new(new MemoryCacheOptions { SizeLimit = 16_000_000 });
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<ProjectionSnapshot?> Get(string scope, CancellationToken ct)
    {
        string? json = memory.Get<string>(scope);
        if (json == null)
        {
            try
            {
                var db = await redis.GetAsync().WaitAsync(TimeSpan.FromMilliseconds(500), ct);
                if (db != null) json = await db.StringGetAsync("projection:v1:" + scope).WaitAsync(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException) { return null; }
        }
        if (json == null || Encoding.UTF8.GetByteCount(json) > ProjectionCodec.MaximumBytes + 1024) return null;
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectionSnapshot>(json, JsonDefaults.Web);
            return snapshot?.Scope == scope ? snapshot : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task Set(ProjectionSnapshot snapshot, CancellationToken ct)
    {
        var json = ProjectionCodec.Serialize(snapshot);
        memory.Set(snapshot.Scope, json, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime, Size = Encoding.UTF8.GetByteCount(json) });
        try
        {
            var db = await redis.GetAsync().WaitAsync(TimeSpan.FromMilliseconds(500), ct);
            if (db != null) await db.StringSetAsync("projection:v1:" + snapshot.Scope, json, Lifetime).WaitAsync(TimeSpan.FromMilliseconds(500), ct);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException) { /* A missing cache only requires a fresh snapshot. */ }
    }
    public void Dispose() => memory.Dispose();
}
