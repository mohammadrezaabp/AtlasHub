using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class ApprovalTests
{
    [Fact]
    public async Task Deletion_requires_a_bound_approval_and_cannot_be_replayed()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        await SignIn(c);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Human approval test content. Keep this source unless approved."), "file", "approval.txt");
        form.Add(new StringContent("Private"), "visibility");
        var upload = await c.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var d = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var id = d.GetProperty("id").GetGuid();
        var request = await c.PostAsJsonAsync($"/api/documents/{id}/delete-request", new { });
        var a = await request.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/documents/{id}")).StatusCode);
        var approval = a.GetProperty("approvalId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/approvals/{approval}", new { approve = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/approvals/{approval}", new { approve = true })).StatusCode);
    }

    [Fact]
    public async Task Only_administrators_can_review_member_deletion_requests()
    {
        using var f = new HubFactory();
        using var admin = f.CreateClient();
        await SignIn(admin);
        await admin.PostAsJsonAsync("/api/admin/users", new { email = "sales@test.local", name = "Sales", password = "Sales-Password-2026!", role = "Member", department = "Sales" });
        using var member = f.CreateClient();
        await SignIn(member, "sales@test.local", "Sales-Password-2026!");
        foreach (var path in new[]
        {
            "/api/approvals",
            "/api/admin/system",
            "/api/models",
            "/api/admin/users"
        }

        )
            Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(path)).StatusCode);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Team storage and administrator review integration fixture."), "file", "review.txt");
        form.Add(new StringContent("Private"), "visibility");
        form.Add(new StringContent("Other team"), "department");
        var uploaded = await member.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var doc = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var id = doc.GetProperty("id").GetGuid();
        var visible = await member.GetFromJsonAsync<JsonElement>("/api/documents");
        Assert.Equal("Sales", visible[0].GetProperty("department").GetString());
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/documents/{id}/download")).StatusCode);
        var proposal = await member.PostAsJsonAsync($"/api/documents/{id}/delete-request", new { });
        var payload = await proposal.Content.ReadFromJsonAsync<JsonElement>();
        var approval = payload.GetProperty("approvalId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/approvals/{approval}", new { approve = true })).StatusCode);
        var queue = await admin.GetFromJsonAsync<JsonElement>("/api/approvals");
        Assert.Contains(queue.EnumerateArray(), a => a.GetProperty("id").GetGuid() == approval);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/approvals/{approval}", new { approve = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/documents/{id}/download")).StatusCode);
    }
}
