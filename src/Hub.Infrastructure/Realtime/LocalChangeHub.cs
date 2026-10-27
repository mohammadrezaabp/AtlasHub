using System.Collections.Concurrent;
using Hub.Application.Abstractions.Realtime;

namespace Hub.Infrastructure.Realtime;

public sealed class LocalChangeHub
{
    private readonly ConcurrentDictionary<Guid, ChannelChangeSubscription> subscribers = new();
    public IChangeSubscription Subscribe(IEnumerable<string> topics)
    {
        var id = Guid.NewGuid();
        var subscription = new ChannelChangeSubscription(topics, () => subscribers.TryRemove(id, out _));
        subscribers[id] = subscription;
        return subscription;
    }
    public void Publish(string topic) { foreach (var subscriber in subscribers.Values) subscriber.Signal(topic); }
}
