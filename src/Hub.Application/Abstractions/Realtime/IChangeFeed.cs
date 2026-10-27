namespace Hub.Application.Abstractions.Realtime;

public interface IChangeFeed
{
    IChangeSubscription Subscribe(IEnumerable<string> topics);
    Task<bool> Publish(string topic, CancellationToken ct = default);
}
