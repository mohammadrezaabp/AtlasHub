using Hub.Infrastructure.Caching;
using StackExchange.Redis;

namespace Hub.Infrastructure.Events;

public sealed class RedisEventMirror(RedisStore redis) : IEventMirror
{
    public async Task Mirror(RunEvent item)
    {
        try
        {
            var cache = await redis.GetAsync();
            if (cache == null) return;
            await cache.StreamAddAsync($"run:{item.RunId}", [new("data", item.Data)], $"{item.Id}-0", maxLength: 5000, useApproximateMaxLength: true);
            await cache.KeyExpireAsync($"run:{item.RunId}", TimeSpan.FromHours(24));
        }
        catch (RedisException) { /* SQL remains the canonical replay log. */ }
    }
}
