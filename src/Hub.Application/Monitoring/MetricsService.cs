using System.Globalization;

namespace Hub.Application.Monitoring;

public sealed class MetricsService(IHubSessionFactory sessions, Hub.Application.Realtime.RealtimeMetrics realtime)
{
    public async Task<bool> Healthy(CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        return await db.CanConnectAsync(ct);
    }

    public async Task<string> Collect(CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var active = await db.CountAsync(db.Runs.Where(r => r.Status == RunStatus.Running), ct);
        var queued = await db.CountAsync(db.Documents.Where(d => d.Status == DocumentStatus.Queued), ct);
        var failures = await db.CountAsync(db.Runs.Where(r => r.Status == RunStatus.Failed), ct);
        var usage = await db.ListAsync(db.Usages.Select(u => new { u.InputTokens, u.OutputTokens, u.CostUsd }), ct);
        return $"# TYPE atlas_active_runs gauge\natlas_active_runs {active}\n# TYPE atlas_ingestion_queue gauge\natlas_ingestion_queue {queued}\n# TYPE atlas_failed_runs_total counter\natlas_failed_runs_total {failures}\n# TYPE atlas_model_tokens_total counter\natlas_model_tokens_total {usage.Sum(u => (long)u.InputTokens + u.OutputTokens)}\n# TYPE atlas_model_cost_usd_total counter\natlas_model_cost_usd_total {usage.Sum(u => u.CostUsd).ToString(CultureInfo.InvariantCulture)}\n" + realtime.Collect();
    }
}
