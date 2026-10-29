using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static Hub.Tests.Support.ApiTestClient;

namespace Hub.Tests.Integration;

public sealed class SecurityBoundaryTests
{
    [Fact]
    public async Task Metrics_require_the_scrape_token()
    {
        using var factory = new HubFactory(backgroundWorkers: false);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/metrics")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/metrics")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", new string('m', 64));
        var scrape = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, scrape.StatusCode);
        Assert.Contains("atlas_", await scrape.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Authentication_keys_honor_the_configured_isolated_storage_directory()
    {
        using var factory = new HubFactory();
        using var client = factory.CreateClient();
        await SignIn(client);
        var options = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var repository = Assert.IsType<FileSystemXmlRepository>(options.XmlRepository);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), repository.Directory.FullName);
        Assert.Contains("atlas-test-", repository.Directory.FullName);
        Assert.NotEmpty(Directory.GetFiles(repository.Directory.FullName, "key-*.xml"));
    }

    [Fact]
    public async Task Public_shell_sets_defensive_headers_without_exposing_runtime_configuration()
    {
        using var factory = new HubFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.DoesNotContain("unsafe-inline", policy);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Test-Admin-Password-2026!", html);
        Assert.DoesNotContain(new string('x', 64), html);
        foreach (var path in new[] { "/.env", "/data/admin-login.txt", "/knowledge/private.txt" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Upload_and_all_mutation_verbs_require_antiforgery_validation()
    {
        using var factory = new HubFactory();
        using var client = factory.CreateClient();
        await SignIn(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent("Sample content"), "file", "sample.txt");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/documents", upload)).StatusCode);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/conversations/{id}", new { title = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/conversations/{id}/pin", new { pinned = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/conversations/{id}")).StatusCode);
    }
}
