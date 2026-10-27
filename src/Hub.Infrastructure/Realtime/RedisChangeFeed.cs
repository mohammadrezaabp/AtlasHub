using Hub.Application.Abstractions.Realtime;
using Hub.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Hub.Infrastructure.Realtime;

public sealed class RedisChangeFeed(RedisStore redis, IConfiguration configuration, LocalChangeHub local) : IChangeFeed
{
    private static readonly RedisChannel Channel = RedisChannel.Literal("atlas:changes:v1");
    private readonly SemaphoreSlim registration = new(1, 1);
    private bool subscribed;

    public IChangeSubscription Subscribe(IEnumerable<string> topics) => local.Subscribe(topics);
    public async Task Connect(CancellationToken ct)
    {
        if (subscribed) return;
        await registration.WaitAsync(ct);
        try
        {
            if (subscribed) return;
            var db = await redis.GetAsync();
            if (db == null) return;
            await db.Multiplexer.GetSubscriber().SubscribeAsync(Channel, (_, message) => local.Publish(message.ToString()));
            subscribed = true;
        }
        finally { registration.Release(); }
    }

    public async Task<bool> Publish(string topic, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        local.Publish(topic);
        if (string.IsNullOrEmpty(configuration["Redis:Connection"])) return true;
        try
        {
            await Connect(ct);
            var db = await redis.GetAsync();
            if (db == null) return false;
            await db.Multiplexer.GetSubscriber().PublishAsync(Channel, topic);
            return true;
        }
        catch (RedisException) { return false; }
    }
}
