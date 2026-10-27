using Microsoft.Extensions.Options;

namespace Hub.Application.Monitoring;

public sealed class UsageAccountingService(IHubSessionFactory sessions, IOptions<BudgetOptions> options) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ModelUsage> Reserve(string actor, string role, string model, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await sessions.CreateAsync(ct);
            var day = DateTime.UtcNow.Date;
            var spent = (await db.ListAsync(db.Usages.Where(u => u.CreatedAt >= day).Select(u => u.CostUsd), ct)).Sum();
            var minute = DateTime.UtcNow.AddMinutes(-1);
            if (await db.CountAsync(db.Usages.Where(u => u.Actor == actor && u.CreatedAt >= minute), ct) >= 40)
                throw new UseCaseException(ErrorKind.RateLimited, "Model request rate exceeded. Wait one minute.");
            var reservation = options.Value.MaxRequestUsd;
            if (spent + reservation > options.Value.DailyUsd)
                throw new UseCaseException(ErrorKind.RateLimited, "The workspace daily AI budget has been reached.");
            var usage = new ModelUsage { Actor = actor, Role = role, Model = model, CostUsd = reservation, Status = ModelUsageStatus.Reserved };
            db.Add(usage);
            await db.SaveChangesAsync(ct);
            return usage;
        }
        finally { gate.Release(); }
    }

    public async Task Settle(ModelUsage usage, CancellationToken ct = default)
    {
        if (usage.Status == ModelUsageStatus.Reserved) { usage.Status = ModelUsageStatus.Rejected; usage.CostUsd = 0; }
        await using var db = await sessions.CreateAsync(ct);
        db.Update(usage);
        await db.SaveChangesAsync(ct);
    }

    public void Dispose() => gate.Dispose();
}
