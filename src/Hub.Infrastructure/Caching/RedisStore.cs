using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
namespace Hub.Infrastructure.Caching;

public sealed class RedisStore(IConfiguration config, ILogger<RedisStore> logger) : IDisposable
{
    private ConnectionMultiplexer? connection;
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<IDatabase?> GetAsync()
    {
        if (string.IsNullOrEmpty(config["Redis:Connection"])) return null;
        if (connection is { IsConnected: true }) return connection.GetDatabase();
        await gate.WaitAsync();
        try
        {
            connection ??= await ConnectionMultiplexer.ConnectAsync(config["Redis:Connection"]!);
            return connection.IsConnected ? connection.GetDatabase() : null;
        }
        catch (RedisException) { logger.LogWarning("Redis unavailable; durable SQL event replay remains active"); return null; }
        finally { gate.Release(); }
    }
    public void Dispose() => connection?.Dispose();
}
