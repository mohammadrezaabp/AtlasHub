using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hub.Tests.Support;

public static class ApiTestClient
{
    public static async Task SignIn(HttpClient client, string email = "admin@test.local", string password = "Test-Admin-Password-2026!")
    {
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Csrf(client);
    }

    public static async Task Csrf(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", result.GetProperty("token").GetString());
    }
}
