namespace Hub.Application.Abstractions.Agents;

public interface IEventStore
{
    Task Append(Guid run, object value, CancellationToken ct = default);
}
