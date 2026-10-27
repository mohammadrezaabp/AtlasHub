using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class KnowledgeAuthorizationTests
{
    [Fact]
    public async Task Private_and_department_documents_never_enter_another_users_search()
    {
        using var f = new HubFactory();
        using var admin = f.CreateClient();
        await SignIn(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/users", new { email = "member@test.local", name = "Member", password = "Member-Password-2026!", role = "Member", department = "Sales" })).StatusCode);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var owner = await db.Users.FirstAsync(u => u.Role == "Admin");
        var docs = new[]
        {
            Document.Create(owner, "Public", "public.txt", "hash1", "text/plain", "Organization", owner.Department),
            Document.Create(owner, "Private", "private.txt", "hash2", "text/plain", "Private", owner.Department),
            Document.Create(owner, "Finance", "finance.txt", "hash3", "text/plain", "Department", "Finance")
        };
        db.Documents.AddRange(docs);
        foreach (var d in docs)
        {
            d.CompleteIndexing(1, null);
            db.Chunks.Add(new() { DocumentId = d.Id, Text = "quasar secret access policy", Section = "Policy" });
        }

        await db.SaveChangesAsync();
        using var member = f.CreateClient();
        await SignIn(member, "member@test.local", "Member-Password-2026!");
        var visible = await member.GetFromJsonAsync<JsonElement>("/api/documents");
        Assert.Single(visible.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync("/api/documents/" + docs[1].Id)).StatusCode);
        var r = await member.PostAsJsonAsync("/api/search", new { query = "quasar" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var hits = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(hits.EnumerateArray());
        Assert.Equal("Public", hits[0].GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/users")).StatusCode);
    }
}
