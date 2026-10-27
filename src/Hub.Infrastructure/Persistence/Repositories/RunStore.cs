using System.Text.Json;
using Hub.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence.Repositories;

public sealed class RunStore(IDbContextFactory<HubDb> factory, IEventMirror mirror) : IRunStore
{
    public async Task<AgentRun?> ClaimNext(string owner, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var next = await db.Runs.AsNoTracking().Where(r => r.Status == RunStatus.Queued).OrderBy(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (next == null) return null;
        next.Start(owner, DateTime.UtcNow.AddMinutes(1));
        var changed = await db.Runs.Where(r => r.Id == next.Id && r.Status == RunStatus.Queued).ExecuteUpdateAsync(s =>
            s.SetProperty(r => r.Status, next.Status).SetProperty(r => r.Owner, next.Owner).SetProperty(r => r.LeaseUntil, next.LeaseUntil), ct);
        return changed == 1 ? next : null;
    }

    public async Task<IReadOnlyList<AgentRun>> RecoverExpired(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var candidates = await db.Runs.AsNoTracking().Where(r => r.Status == RunStatus.Running && r.LeaseUntil < DateTime.UtcNow).ToListAsync(ct);
        var recovered = new List<AgentRun>();
        foreach (var run in candidates)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await db.Runs.Where(r => r.Id == run.Id && r.Status == RunStatus.Running && r.LeaseUntil < DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RunStatus.Interrupted), ct) == 0) continue;
            RunEvent[] terminalEvents = [
                new() { RunId = run.Id, Data = JsonSerializer.Serialize(new { type = "error", message = "The answer was interrupted by a service restart. Send the question again." }, JsonDefaults.Web) },
                new() { RunId = run.Id, Data = JsonSerializer.Serialize(new { type = "run_finished", status = RunStatus.Interrupted }, JsonDefaults.Web) }];
            db.Events.AddRange(terminalEvents);
            await db.SaveChangesAsync(ct);
            await db.Conversations.Where(c => c.Id == run.ConversationId && c.ActiveRunId == run.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveRunId, (Guid?)null), ct);
            await tx.CommitAsync(ct);
            foreach (var item in terminalEvents) await mirror.Mirror(item);
            recovered.Add(run);
        }
        return recovered;
    }

    public async Task<bool> Heartbeat(Guid id, string owner, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Runs.AnyAsync(r => r.Id == id && (r.CancelRequested || r.Owner != owner), ct)) return false;
        return await db.Runs.Where(r => r.Id == id && r.Owner == owner && r.Status == RunStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LeaseUntil, DateTime.UtcNow.AddMinutes(1)), ct) == 1;
    }

    public async Task Complete(AgentRun run, string owner, string status, string answer, IReadOnlyList<Citation> sources, CancellationToken ct = default)
    {
        if (!RunStatus.IsTerminal(status)) throw new Hub.Domain.Common.DomainRuleException("An answer must finish with a terminal status.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Runs.Where(r => r.Id == run.Id && r.Owner == owner && r.Status == RunStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status).SetProperty(r => r.LeaseUntil, (DateTime?)null), ct) == 0) return;
        if (answer.Length > 0) db.Messages.Add(new Message { ConversationId = run.ConversationId, Role = "assistant", Text = answer, Sources = JsonSerializer.Serialize(sources, JsonDefaults.Web), RunId = run.Id });
        var finished = new RunEvent { RunId = run.Id, Data = JsonSerializer.Serialize(new { type = "run_finished", status, sources }, JsonDefaults.Web) };
        db.Events.Add(finished);
        await db.SaveChangesAsync(ct);
        await db.Conversations.Where(c => c.Id == run.ConversationId && c.ActiveRunId == run.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveRunId, (Guid?)null), ct);
        await tx.CommitAsync(ct);
        await mirror.Mirror(finished);
    }
}
