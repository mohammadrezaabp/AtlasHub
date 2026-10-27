using System.Data;
using Hub.Application.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence.Repositories;

public sealed class ConversationStore(IDbContextFactory<HubDb> factory) : IConversationStore
{
    public async Task<bool> Rename(Guid id, Guid owner, string title, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == owner, ct);
        if (conversation == null) return false;
        conversation.Rename(title);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetPinned(Guid id, Guid owner, bool pinned, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Conversations.AnyAsync(c => c.Id == id && c.UserId == owner, ct)) return false;
        var key = $"chat.pin:{owner}:{id}";
        if (pinned)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Settings\" (\"Key\", \"Value\") VALUES ({key}, {id.ToString()}) ON CONFLICT (\"Key\") DO NOTHING", ct);
        else await db.Settings.Where(s => s.Key == key).ExecuteDeleteAsync(ct);
        db.QueueRealtimeChange(RealtimeTopics.Conversations(owner));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> Delete(Guid id, Guid owner, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == owner, ct);
        if (conversation == null) return false;
        conversation.EnsureCanDelete();
        var runs = db.Runs.Where(r => r.ConversationId == id).Select(r => r.Id);
        await db.Events.Where(e => runs.Contains(e.RunId)).ExecuteDeleteAsync(ct);
        await db.Messages.Where(m => m.ConversationId == id).ExecuteDeleteAsync(ct);
        await db.Runs.Where(r => r.ConversationId == id).ExecuteDeleteAsync(ct);
        foreach (var voice in await db.VoiceAssets.Where(v => v.ConversationId == id).ToListAsync(ct))
            db.Settings.Add(new Setting { Key = $"storage.delete:voice:{voice.Id}", Value = voice.Path });
        await db.VoiceAssets.Where(v => v.ConversationId == id).ExecuteDeleteAsync(ct);
        await db.Conversations.Where(c => c.Id == id && c.UserId == owner).ExecuteDeleteAsync(ct);
        var pin = $"chat.pin:{owner}:{id}";
        await db.Settings.Where(s => s.Key == pin).ExecuteDeleteAsync(ct);
        db.Audit.Add(new AuditEntry { Actor = owner.ToString(), Action = "ConversationDeleted", Detail = id.ToString() });
        db.QueueRealtimeChange(RealtimeTopics.Conversations(owner));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> StartRun(Conversation conversation, AgentRun run, bool isNew, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (isNew)
        {
            conversation.BeginRun(run.Id);
            db.Conversations.Add(conversation);
        }
        else if (await db.Conversations.Where(c => c.Id == conversation.Id && c.UserId == run.UserId && c.ActiveRunId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveRunId, run.Id), ct) == 0) return false;
        db.Runs.Add(run);
        db.Messages.Add(new Message { ConversationId = conversation.Id, Role = "user", Text = run.Prompt, RunId = run.Id });
        db.QueueRealtimeChange(RealtimeTopics.Conversations(run.UserId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RequestStop(Guid runId, Guid owner, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Runs.Where(r => r.Id == runId && r.UserId == owner && (r.Status == RunStatus.Running || r.Status == RunStatus.Queued))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CancelRequested, true), ct) == 1;
    }
}
