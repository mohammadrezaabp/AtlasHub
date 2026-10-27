using System.Text.Json;

namespace Hub.Tests.Support;

public sealed class SseTestReader(HttpResponseMessage response, StreamReader reader) : IDisposable
{
    public static async Task<SseTestReader> Open(HttpClient client, string path, CancellationToken ct)
    {
        var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return new SseTestReader(response, new StreamReader(await response.Content.ReadAsStreamAsync(ct)));
    }

    public async Task<JsonElement> Next(CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            using var document = JsonDocument.Parse(line[6..]);
            return document.RootElement.Clone();
        }
        throw new EndOfStreamException("The event stream ended before the expected frame.");
    }
    public void Dispose() { reader.Dispose(); response.Dispose(); }
}
