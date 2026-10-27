using System.Collections.Concurrent;

namespace Hub.Web.Workers;

public sealed class AgentWorker(IRunStore runs, AgentRunProcessor processor, ILogger<AgentWorker> logger) : BackgroundService
{
    private readonly string owner = Guid.NewGuid().ToString();
    private readonly ConcurrentDictionary<Guid, Task> active = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await runs.RecoverExpired(ct);
                if (active.Count < 2)
                {
                    var next = await runs.ClaimNext(owner, ct);
                    if (next != null) active[next.Id] = processor.Execute(next, owner, ct);
                }
                foreach (var done in active.Where(p => p.Value.IsCompleted).ToArray())
                    if (active.TryRemove(done.Key, out var completed)) await completed;
                await Task.Delay(500, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Agent queue retry after {Type}", ex.GetType().Name);
                await Task.Delay(2000, ct);
            }
        }
        await Task.WhenAll(active.Values);
    }
}
