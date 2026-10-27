using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class ConversationTests
{
    [Fact]
    public async Task SSE_events_are_scoped_to_the_run_owner()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        await SignIn(c);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var stranger = Guid.NewGuid();
        var run = AgentRun.Create(stranger, Guid.NewGuid(), "Foreign question");
        run.Start("test-owner", DateTime.UtcNow.AddMinutes(1));
        run.Finish(RunStatus.Succeeded);
        db.Runs.Add(run);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/runs/{run.Id}/events")).StatusCode);
    }

    [Fact]
    public async Task Chat_management_is_owner_scoped_and_active_chats_cannot_be_deleted()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        await SignIn(c);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.FirstAsync(u => u.Role == "Admin");
        var mine = Conversation.Create(owner.Id, "Before");
        var foreign = Conversation.Create(Guid.NewGuid(), "Private chat");
        var active = Conversation.Create(owner.Id, "Active");
        active.BeginRun(Guid.NewGuid());
        db.Conversations.AddRange(mine, foreign, active);
        var run = AgentRun.Create(owner.Id, mine.Id, "Completed question");
        db.Runs.Add(run);
        db.Events.Add(new() { RunId = run.Id, Data = "{}" });
        db.Messages.Add(new() { ConversationId = mine.Id, Text = "Temporary test message" });
        await db.SaveChangesAsync();
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var voice = new VoiceAsset
        {
            OwnerId = owner.Id,
            ConversationId = mine.Id
        };
        voice.Path = await files.Put(StorageKeys.Voice(owner, voice.Id, "speech", ".mp3"), new byte[] { 1, 2, 3 }, "audio/mpeg");
        db.VoiceAssets.Add(voice);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PatchAsJsonAsync($"/api/conversations/{foreign.Id}", new { title = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/conversations/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"/api/conversations/{active.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsJsonAsync($"/api/conversations/{mine.Id}", new { title = "Renamed chat" })).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal("Renamed chat", (await db.Conversations.FindAsync(mine.Id))!.Title);
        Assert.Equal(HttpStatusCode.OK, (await c.DeleteAsync($"/api/conversations/{mine.Id}")).StatusCode);
        Assert.False(await db.Events.AnyAsync(e => e.RunId == run.Id));
        Assert.False(await db.Messages.AnyAsync(m => m.ConversationId == mine.Id));
        Assert.False(await db.VoiceAssets.AnyAsync(v => v.Id == voice.Id));
        await Assert.ThrowsAsync<FileNotFoundException>(() => files.Read(voice.Path));
    }

    [Fact]
    public async Task Chat_pins_persist_are_owner_scoped_and_precede_recent_chats()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        await SignIn(c);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.FirstAsync(u => u.Role == "Admin");
        var old = Conversation.Create(owner.Id, "Pinned older chat");
        old.CreatedAt = DateTime.UtcNow.AddYears(-1);
        var foreign = Conversation.Create(Guid.NewGuid(), "Foreign chat");
        db.Conversations.AddRange(old, foreign);
        db.Conversations.AddRange(Enumerable.Range(0, 105).Select(i => Conversation.Create(owner.Id, $"Recent {i}")));
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/conversations/{foreign.Id}/pin", new { pinned = true })).StatusCode);
        using var noCsrf = f.CreateClient();
        await SignIn(noCsrf);
        noCsrf.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await noCsrf.PutAsJsonAsync($"/api/conversations/{old.Id}/pin", new { pinned = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/conversations/{old.Id}/pin", new { pinned = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/conversations/{old.Id}/pin", new { pinned = true })).StatusCode);
        using var reconnected = f.CreateClient();
        await SignIn(reconnected);
        var list = await reconnected.GetFromJsonAsync<JsonElement>("/api/conversations");
        Assert.Equal(100, list.GetArrayLength());
        Assert.Equal(old.Id, list[0].GetProperty("id").GetGuid());
        Assert.True(list[0].GetProperty("pinned").GetBoolean());
        Assert.DoesNotContain(list.EnumerateArray(), x => x.GetProperty("id").GetGuid() == foreign.Id);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/conversations/{old.Id}/pin", new { pinned = false })).StatusCode);
        Assert.False(await db.Settings.AnyAsync(s => s.Key == $"chat.pin:{owner.Id}:{old.Id}"));
        await c.PutAsJsonAsync($"/api/conversations/{old.Id}/pin", new { pinned = true });
        await c.DeleteAsync($"/api/conversations/{old.Id}");
        Assert.False(await db.Settings.AnyAsync(s => s.Key == $"chat.pin:{owner.Id}:{old.Id}"));
    }
}
