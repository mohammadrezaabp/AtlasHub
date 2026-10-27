using System.Threading.Channels;
using Hub.Application.Abstractions.Realtime;

namespace Hub.Infrastructure.Realtime;

public sealed class ChannelChangeSubscription(IEnumerable<string> topics, Action onDispose) : IChangeSubscription
{
    private readonly HashSet<string> interests = topics.ToHashSet(StringComparer.Ordinal);
    private readonly Channel<bool> pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private int disposed;

    public void Signal(string topic) { if (interests.Contains(topic)) pending.Writer.TryWrite(true); }
    public async Task<bool> Wait(TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try { return await pending.Reader.ReadAsync(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (ChannelClosedException) { return false; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        onDispose();
        pending.Writer.TryComplete();
    }
}
