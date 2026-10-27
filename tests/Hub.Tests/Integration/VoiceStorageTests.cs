using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class VoiceStorageTests
{
    [Fact]
    public async Task Stored_voice_playback_is_private_and_supports_ranges()
    {
        using var f = new HubFactory();
        using var admin = f.CreateClient();
        await SignIn(admin);
        await admin.PostAsJsonAsync("/api/admin/users", new { email = "voice@test.local", name = "Voice", password = "Voice-Password-2026!", role = "Member", department = "Audio" });
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var owner = await db.Users.FirstAsync(u => u.Email == "voice@test.local");
        var bytes = new byte[]
        {
            1,
            2,
            3,
            4,
            5,
            6
        };
        var own = new VoiceAsset
        {
            OwnerId = owner.Id,
            Department = owner.Department,
            ContentType = "audio/mpeg"
        };
        own.Path = await files.Put(StorageKeys.Voice(owner, own.Id, "speech", ".mp3"), bytes, "audio/mpeg");
        var foreign = new VoiceAsset
        {
            OwnerId = Guid.NewGuid(),
            Path = own.Path
        };
        db.VoiceAssets.AddRange(own, foreign);
        await db.SaveChangesAsync();
        using var member = f.CreateClient();
        await SignIn(member, "voice@test.local", "Voice-Password-2026!");
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/voices/{foreign.Id}/audio")).StatusCode);
        Assert.Equal(bytes, await member.GetByteArrayAsync($"/api/voices/{own.Id}/audio"));
        using var range = new HttpRequestMessage(HttpMethod.Get, $"/api/voices/{own.Id}/audio");
        range.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1, 3);
        var partial = await member.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(new byte[] { 2, 3, 4 }, await partial.Content.ReadAsByteArrayAsync());
        var list = await member.GetFromJsonAsync<JsonElement>("/api/voices");
        Assert.Single(list.EnumerateArray());
    }
}
