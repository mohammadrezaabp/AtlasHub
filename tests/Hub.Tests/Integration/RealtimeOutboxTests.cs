using Hub.Application.Abstractions.Realtime;
using Hub.Application.Realtime;
using Hub.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hub.Tests.Integration;

public sealed class RealtimeOutboxTests
{
    [Fact]
    public async Task Rolled_back_changes_do_not_leave_outbox_notifications()
    {
        using var factory = new HubFactory(backgroundWorkers: false, realtimeDelivery: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.SingleAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Documents.Add(Document.Create(owner, "Rolled back", "rollback.txt", "hash", "text/plain", "Private", owner.Department));
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Documents.ToListAsync());
        Assert.False(await db.RealtimeOutbox.AnyAsync(entry => entry.Topic == RealtimeTopics.Documents));
    }

    [Fact]
    public async Task Atomic_pin_updates_enqueue_a_committed_owner_scoped_notification()
    {
        using var factory = new HubFactory(backgroundWorkers: false, realtimeDelivery: false);
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.SingleAsync();
        var chat = Conversation.Create(owner.Id, "Pin");
        db.Conversations.Add(chat);
        await db.SaveChangesAsync();
        await db.RealtimeOutbox.ExecuteDeleteAsync();
        var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
        Assert.True(await store.SetPinned(chat.Id, owner.Id, true, default));
        Assert.Equal(RealtimeTopics.Conversations(owner.Id), (await db.RealtimeOutbox.AsNoTracking().SingleAsync()).Topic);
    }

    [Fact]
    public async Task Signals_are_scoped_coalesced_and_waits_are_cancellable()
    {
        var hub = new LocalChangeHub();
        using var interested = hub.Subscribe(["documents"]);
        using var other = hub.Subscribe(["conversations:other"]);
        for (var index = 0; index < 100; index++) hub.Publish("documents");
        Assert.True(await interested.Wait(TimeSpan.FromSeconds(1), default));
        Assert.False(await interested.Wait(TimeSpan.FromMilliseconds(20), default));
        Assert.False(await other.Wait(TimeSpan.FromMilliseconds(20), default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interested.Wait(TimeSpan.FromSeconds(1), cancellation.Token));
    }
}
