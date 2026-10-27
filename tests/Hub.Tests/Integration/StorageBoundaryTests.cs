using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class StorageBoundaryTests
{
    [Fact]
    public async Task Storage_keys_keep_team_and_visibility_boundaries_and_reject_traversal()
    {
        using var f = new HubFactory();
        using var c = f.CreateClient();
        await SignIn(c);
        using var scope = f.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var d = Document.Create(HubUser.Create("test@example.com", "Test", "Member", "Sales / West"), "Policy", "../../policy.txt", "hash", "text/plain", "Private", "Sales / West");
        var key = StorageKeys.Document(d);
        Assert.StartsWith("teams/sales-west-", key);
        Assert.Contains($"/documents/private/{d.OwnerId:N}/", key);
        Assert.EndsWith("/policy.txt", key);
        Assert.DoesNotContain("..", key);
        var bytes = "Exact storage roundtrip"u8.ToArray();
        var location = await files.Put(key, bytes, "text/plain");
        Assert.Equal(bytes, await files.Read(location));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.Put("../escape.txt", bytes, "text/plain"));
        await files.Remove(location);
        await Assert.ThrowsAsync<FileNotFoundException>(() => files.Read(location));
        Assert.NotEqual(StorageKeys.Team("Sales / West"), StorageKeys.Team("Sales - West"));
    }
}
