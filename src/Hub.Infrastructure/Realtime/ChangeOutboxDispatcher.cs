using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hub.Infrastructure.Realtime;

public sealed class ChangeOutboxDispatcher(IDbContextFactory<HubDb> sessions, RedisChangeFeed feed, ILogger<ChangeOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await feed.Connect(stoppingToken);
                await using var db = await sessions.CreateDbContextAsync(stoppingToken);
                var batch = await db.RealtimeOutbox.AsNoTracking().OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id).Take(128).ToListAsync(stoppingToken);
                foreach (var topic in batch.Select(entry => entry.Topic).Distinct())
                {
                    if (!await feed.Publish(topic, stoppingToken)) continue;
                    var ids = batch.Where(entry => entry.Topic == topic).Select(entry => entry.Id).ToList();
                    await db.RealtimeOutbox.Where(entry => ids.Contains(entry.Id)).ExecuteDeleteAsync(stoppingToken);
                }
                await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogWarning("Realtime outbox retries after {Type}", error.GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
