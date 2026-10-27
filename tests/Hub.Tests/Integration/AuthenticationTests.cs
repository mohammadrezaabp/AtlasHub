using System.Net;
using System.Net.Http.Json;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Anonymous_data_and_CSRF_mutations_are_rejected()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@test.local", password = "Test-Admin-Password-2026!" })).StatusCode);
        await SignIn(c);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/chat", new { message = "test" })).StatusCode);
    }
}
