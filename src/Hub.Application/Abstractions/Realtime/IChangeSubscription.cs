namespace Hub.Application.Abstractions.Realtime;

public interface IChangeSubscription : IDisposable
{
    Task<bool> Wait(TimeSpan timeout, CancellationToken ct);
}
