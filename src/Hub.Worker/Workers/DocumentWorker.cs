namespace Hub.Worker.Workers;

public sealed class DocumentWorker(IHubSessionFactory factory, IngestionService ingest, SeedImporter seed, IStorageMaintenance storage, ILogger<DocumentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var db = await factory.CreateAsync(ct);
                if (await db.AnyAsync(db.Users, ct))
                    break;
            }
            catch (Exception)
            {
            }

            await Task.Delay(3000, ct);
        }

        await seed.Import(ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var db = await factory.CreateAsync(ct);
                var heartbeat = await db.FirstOrDefaultAsync(db.Settings.Where(s => s.Key == "worker.heartbeat"), ct);
                if (heartbeat == null)
                    db.Add<Setting>(new() { Key = "worker.heartbeat", Value = DateTime.UtcNow.ToString("O") });
                else
                    heartbeat.Value = DateTime.UtcNow.ToString("O");
                await db.SaveChangesAsync(ct);
                await storage.DrainDeletes(ct);
                if (!await ingest.ProcessNext(ct))
                    await Task.Delay(2500, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogWarning("Worker retry after {Type}", e.GetType().Name);
                await Task.Delay(5000, ct);
            }
        }
    }
}
