using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class ProviderWorkflowTests
{
    [Fact]
    public async Task Speech_workflows_store_private_assets_and_reject_foreign_conversations()
    {
        var provider = new StubSpeechProvider();
        using var parent = new HubFactory(backgroundWorkers: false);
        using var factory = parent.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ISpeechProvider>(); s.AddSingleton<ISpeechProvider>(provider); }));
        using var client = factory.CreateClient();
        await SignIn(client);
        var response = await client.PostAsJsonAsync("/api/speech/synthesize", new { text = "Stored speech" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(provider.Audio, await response.Content.ReadAsByteArrayAsync());
        var audioId = Guid.Parse(response.Headers.GetValues("X-Audio-Id").Single());
        Assert.Equal(provider.Audio, await client.GetByteArrayAsync($"/api/voices/{audioId}/audio"));
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(provider.Audio);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "file", "recording.mp3");
        var transcript = await client.PostAsync("/api/speech/transcribe", form);
        Assert.Equal(HttpStatusCode.OK, transcript.StatusCode);
        Assert.Equal("Saved recording", (await transcript.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("transcript").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var assets = await db.VoiceAssets.ToListAsync();
        Assert.Equal(2, assets.Count);
        Assert.All(assets, asset => Assert.Contains("/voices/private/", asset.Path.Replace('\\', '/')));
        var foreign = Conversation.Create(Guid.NewGuid(), "Foreign");
        db.Conversations.Add(foreign);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/speech/synthesize", new { text = "Denied", conversationId = foreign.Id })).StatusCode);
        Assert.Equal(2, await db.VoiceAssets.CountAsync());
    }

    [Fact]
    public async Task Ingestion_retains_every_chunk_when_embedding_provider_fails()
    {
        var models = new StubModelClient { FailEmbeddings = true };
        using var parent = new HubFactory(backgroundWorkers: false);
        using var factory = parent.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IModelClient>(); s.AddSingleton<IModelClient>(models); }));
        using var client = factory.CreateClient();
        await SignIn(client);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("# Policy\n" + string.Join(' ', Enumerable.Repeat("Exact source evidence.", 3000))), "file", "policy.txt");
        var uploaded = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.True(await factory.Services.GetRequiredService<IngestionService>().ProcessNext(default));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var document = await db.Documents.SingleAsync();
        Assert.Equal("LexicalOnly", document.Status);
        Assert.True(document.ChunkCount > 16);
        Assert.Equal(document.ChunkCount, await db.Chunks.CountAsync());
        Assert.Equal(1, models.EmbeddingCalls);
    }
}
