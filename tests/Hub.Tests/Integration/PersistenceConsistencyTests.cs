using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hub.Tests.Integration;

public sealed class PersistenceConsistencyTests
{
    [Fact]
    public async Task Concurrent_answer_starts_claim_one_chat_and_persist_one_user_message()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var user = await db.Users.SingleAsync();
        var conversation = Conversation.Create(user.Id, "Concurrency");
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
        var first = AgentRun.Create(user.Id, conversation.Id, "First");
        var second = AgentRun.Create(user.Id, conversation.Id, "Second");
        var accepted = await Task.WhenAll(store.StartRun(conversation, first, false, default), store.StartRun(conversation, second, false, default));
        Assert.Single(accepted, x => x);
        Assert.Single(await db.Runs.ToListAsync());
        Assert.Single(await db.Messages.ToListAsync());
    }

    [Fact]
    public async Task Failed_chunk_replacement_rolls_back_to_the_previous_searchable_document()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.SingleAsync();
        var document = Document.Create(owner, "Policy", "policy.txt", "hash", "text/plain", "Private", owner.Department);
        document.CompleteIndexing(1, null);
        db.Documents.Add(document);
        var old = new Chunk { DocumentId = document.Id, Text = "Preserved source", Position = 0 };
        db.Chunks.Add(old);
        await db.SaveChangesAsync();
        var index = scope.ServiceProvider.GetRequiredService<IDocumentIndexStore>();
        Chunk[] invalid = [new() { DocumentId = document.Id, Position = 0, Text = "New" }, new() { DocumentId = document.Id, Position = 0, Text = "Duplicate position" }];
        await Assert.ThrowsAsync<DbUpdateException>(() => index.Complete(document.Id, invalid, null, default));
        db.ChangeTracker.Clear();
        Assert.Equal(old.Id, (await db.Chunks.SingleAsync()).Id);
        Assert.Equal("Ready", (await db.Documents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Run_completion_is_idempotent_and_terminal_event_is_committed_with_the_answer()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var user = await db.Users.SingleAsync();
        var conversation = Conversation.Create(user.Id, "Completion");
        var run = AgentRun.Create(user.Id, conversation.Id, "Question");
        await scope.ServiceProvider.GetRequiredService<IConversationStore>().StartRun(conversation, run, true, default);
        var runs = scope.ServiceProvider.GetRequiredService<IRunStore>();
        var claimed = await runs.ClaimNext("test-owner", default);
        Assert.NotNull(claimed);
        await runs.Complete(claimed, "test-owner", "Succeeded", "Answer", []);
        await runs.Complete(claimed, "test-owner", "Succeeded", "Duplicate", []);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Conversations.SingleAsync()).ActiveRunId);
        Assert.Single(await db.Messages.Where(m => m.Role == "assistant").ToListAsync());
        Assert.Single(await db.Events.ToListAsync());
        Assert.Equal("Succeeded", (await db.Runs.SingleAsync()).Status);
    }

    [Fact]
    public async Task Expired_runs_release_the_chat_and_persist_replayable_terminal_events_once()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var user = await db.Users.SingleAsync();
        var conversation = Conversation.Create(user.Id, "Expired");
        var run = AgentRun.Create(user.Id, conversation.Id, "Expired question");
        run.Start("expired-owner", DateTime.UtcNow.AddMinutes(-2));
        conversation.BeginRun(run.Id);
        db.Conversations.Add(conversation);
        db.Runs.Add(run);
        await db.SaveChangesAsync();
        var runs = scope.ServiceProvider.GetRequiredService<IRunStore>();
        Assert.Single(await runs.RecoverExpired(default));
        Assert.Empty(await runs.RecoverExpired(default));
        db.ChangeTracker.Clear();
        Assert.Null((await db.Conversations.SingleAsync()).ActiveRunId);
        Assert.Equal(2, await db.Events.CountAsync());
        Assert.Equal("Interrupted", (await db.Runs.SingleAsync()).Status);
    }
}
