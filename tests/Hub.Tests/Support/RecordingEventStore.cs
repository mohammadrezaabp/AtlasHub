using Hub.Application.Abstractions.Agents;

namespace Hub.Tests.Support;

public sealed class RecordingEventStore : IEventStore
{
    public List<object> Events { get; } = [];
    public Task Append(Guid run, object value, CancellationToken ct = default)
    {
        Events.Add(value);
        return Task.CompletedTask;
    }
}
