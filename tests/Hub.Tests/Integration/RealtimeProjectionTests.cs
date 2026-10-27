using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hub.Application.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class RealtimeProjectionTests
{
    [Fact]
    public async Task Reconnect_revalidates_the_view_and_resumes_without_resending_data()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await SignIn(client);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string fingerprint;
        using (var first = await SseTestReader.Open(client, "/api/realtime/documents", deadline.Token))
        {
            var frame = await first.Next(deadline.Token);
            Assert.Equal("snapshot", frame.GetProperty("type").GetString());
            fingerprint = frame.GetProperty("fingerprint").GetString()!;
        }
        using var resumed = await SseTestReader.Open(client, "/api/realtime/documents?fingerprint=" + fingerprint, deadline.Token);
        var response = await resumed.Next(deadline.Token);
        Assert.Equal("resumed", response.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("data").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/realtime/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/realtime/documents?fingerprint=invalid")).StatusCode);
    }

    [Fact]
    public async Task Hidden_changes_are_suppressed_and_cache_state_never_crosses_user_boundaries()
    {
        using var factory = new HubFactory(backgroundWorkers: false, realtimeDelivery: false);
        using var client = factory.CreateClient();
        await SignIn(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var admin = await db.Users.SingleAsync();
        var member = HubUser.Create("viewer@test.local", "Viewer", "Member", "Sales");
        var hidden = Document.Create(admin, "Private policy", "private.txt", "hash", "text/plain", "Private", admin.Department);
        db.Users.Add(member);
        db.Documents.Add(hidden);
        await db.SaveChangesAsync();
        var projections = scope.ServiceProvider.GetRequiredService<ProjectionService>();
        var memberView = await projections.Read(member.Id, "0", "documents", null, null, default);
        var adminView = await projections.Read(admin.Id, "0", "documents", null, null, default);
        hidden.Title = "Changed private policy";
        await db.SaveChangesAsync();
        var unchanged = await projections.Read(member.Id, "0", "documents", null, memberView.Snapshot, default);
        Assert.Null(unchanged.Frame);
        var forged = await projections.Read(member.Id, "0", "documents", adminView.Snapshot.Fingerprint, null, default);
        Assert.Equal("snapshot", forged.Frame!.Type);
        Assert.Equal("[]", forged.Frame.Data!.ToJsonString());
        Assert.NotEqual(adminView.Snapshot.Scope, memberView.Snapshot.Scope);
    }

    [Fact]
    public async Task Committed_document_changes_deliver_patches_and_reconnect_can_use_the_cached_base()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await SignIn(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.SingleAsync();
        var documents = Enumerable.Range(0, 20).Select(index => Document.Create(owner, $"Policy {index}", $"policy-{index}.txt", $"hash-{index}", "text/plain", "Organization", owner.Department)).ToList();
        db.Documents.AddRange(documents);
        await db.SaveChangesAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stream = await SseTestReader.Open(client, "/api/realtime/documents", deadline.Token);
        var snapshot = await stream.Next(deadline.Token);
        documents[0].CompleteIndexing(3, null);
        await db.SaveChangesAsync();
        var patch = await stream.Next(deadline.Token);
        Assert.Equal("patch", patch.GetProperty("type").GetString());
        Assert.Equal(snapshot.GetProperty("fingerprint").GetString(), patch.GetProperty("baseFingerprint").GetString());
        Assert.Contains(patch.GetProperty("patch").EnumerateArray(), operation => operation.GetProperty("path").GetString()!.EndsWith("/status"));
        stream.Dispose();
        var projections = scope.ServiceProvider.GetRequiredService<ProjectionService>();
        var current = await projections.Read(owner.Id, "0", "documents", null, null, default);
        documents[1].CompleteIndexing(2, null);
        await db.SaveChangesAsync();
        using var reconnected = await SseTestReader.Open(client, "/api/realtime/documents?fingerprint=" + current.Snapshot.Fingerprint, deadline.Token);
        Assert.Equal("patch", (await reconnected.Next(deadline.Token)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Password_changes_close_an_already_open_projection_stream()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        await SignIn(client);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stream = await SseTestReader.Open(client, "/api/realtime/documents", deadline.Token);
        await stream.Next(deadline.Token);
        var response = await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "Test-Admin-Password-2026!", newPassword = "Changed-Password-2026!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await stream.Next(deadline.Token);
        Assert.Equal("subscription_closed", closed.GetProperty("type").GetString());
        Assert.Equal("session_revoked", closed.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Lost_notifications_recover_current_state_through_the_bounded_fallback()
    {
        using var factory = new HubFactory(backgroundWorkers: false, realtimeDelivery: false);
        using var client = factory.CreateClient();
        await SignIn(client);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var stream = await SseTestReader.Open(client, "/api/realtime/documents", deadline.Token);
        await stream.Next(deadline.Token);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var user = await db.Users.SingleAsync();
        db.Documents.Add(Document.Create(user, "Fallback", "fallback.txt", "hash", "text/plain", "Organization", user.Department));
        await db.SaveChangesAsync();
        var changed = await stream.Next(deadline.Token);
        Assert.NotEqual("resumed", changed.GetProperty("type").GetString());
        Assert.Contains("Fallback", changed.GetRawText());
    }

    [Fact]
    public async Task Disabling_an_account_closes_an_open_answer_before_delivering_further_events()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var admin = factory.CreateClient();
        await SignIn(admin);
        var response = await admin.PostAsJsonAsync("/api/admin/users", new { email = "member@test.local", name = "Member", password = "Member-Password-2026!", role = "Member", department = "Sales" });
        var member = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var client = factory.CreateClient();
        await SignIn(client, "member@test.local", "Member-Password-2026!");
        var started = await client.PostAsJsonAsync("/api/chat", new { message = "Question" });
        var run = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stream = await SseTestReader.Open(client, $"/api/runs/{run}/events", deadline.Token);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/admin/users/{member}/enabled", new { enabled = false })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        db.Events.Add(new RunEvent { RunId = run, Data = "{\"type\":\"text_delta\",\"delta\":\"must not be delivered\"}" });
        await db.SaveChangesAsync();
        var closed = await stream.Next(deadline.Token);
        Assert.Equal("subscription_closed", closed.GetProperty("type").GetString());
    }
}
