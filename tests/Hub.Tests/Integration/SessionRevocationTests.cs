using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class SessionRevocationTests
{
    [Fact]
    public async Task Password_changes_revoke_other_sessions_and_require_the_new_password()
    {
        using var factory = new HubFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        await SignIn(first);
        await SignIn(second);
        var changed = await first.PostAsJsonAsync("/api/auth/password", new { currentPassword = "Test-Admin-Password-2026!", newPassword = "Changed-Password-2026!" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/me")).StatusCode);
        await SignIn(second, password: "Changed-Password-2026!");
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Disabling_a_member_revokes_access_and_admin_cannot_disable_self()
    {
        using var factory = new HubFactory();
        using var admin = factory.CreateClient();
        await SignIn(admin);
        var created = await admin.PostAsJsonAsync("/api/admin/users", new { email = "member@test.local", name = "Member", password = "Member-Password-2026!", role = "Member", department = "Sales" });
        var memberId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var member = factory.CreateClient();
        await SignIn(member, "member@test.local", "Member-Password-2026!");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/admin/users/{memberId}/enabled", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/documents")).StatusCode);
        var current = await admin.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/users/{current.GetProperty("id").GetGuid()}/enabled", new { enabled = false })).StatusCode);
    }

    [Fact]
    public async Task Invalid_account_details_return_validation_errors()
    {
        using var factory = new HubFactory();
        using var admin = factory.CreateClient();
        await SignIn(admin);
        var response = await admin.PostAsJsonAsync("/api/admin/users", new { email = "invalid", name = "Member", password = "Member-Password-2026!", role = "Member", department = "Sales" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
