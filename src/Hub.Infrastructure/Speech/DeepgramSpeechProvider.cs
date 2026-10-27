using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.Speech;

public sealed class DeepgramSpeechProvider(IHttpClientFactory clients, IConfiguration config) : ISpeechProvider
{
    public async Task<Transcription> Transcribe(byte[] bytes, string mime, CancellationToken ct)
    {
        using var request = Request("listen?model=nova-3&language=en&smart_format=true&mip_opt_out=true");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        using var response = await clients.CreateClient("external").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw Unavailable("transcription", response);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        var alternative = document?["results"]?["channels"]?[0]?["alternatives"]?[0];
        return new Transcription(alternative?["transcript"]?.ToString() ?? "", alternative?["confidence"]?.GetValue<double>() ?? 0);
    }

    public async Task<byte[]> Synthesize(string text, CancellationToken ct)
    {
        using var request = Request("speak?model=aura-2-thalia-en&encoding=mp3&mip_opt_out=true");
        request.Content = JsonContent.Create(new { text });
        using var response = await clients.CreateClient("external").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw Unavailable("speech output", response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private HttpRequestMessage Request(string endpoint)
    {
        var key = config["Deepgram:ApiKey"];
        if (string.IsNullOrEmpty(key)) throw new UseCaseException(ErrorKind.Validation, "Configure your Deepgram API key.");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepgram.com/v1/" + endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", key);
        return request;
    }

    private static UseCaseException Unavailable(string operation, HttpResponseMessage response) =>
        new(ErrorKind.Unavailable, $"Deepgram {operation} unavailable ({(int)response.StatusCode}).");
}
