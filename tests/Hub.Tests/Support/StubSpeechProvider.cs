namespace Hub.Tests.Support;

public sealed class StubSpeechProvider : ISpeechProvider
{
    public byte[] Audio { get; } = [4, 8, 15, 16, 23, 42];
    public Task<Transcription> Transcribe(byte[] bytes, string mime, CancellationToken ct) => Task.FromResult(new Transcription("Saved recording", 0.99));
    public Task<byte[]> Synthesize(string text, CancellationToken ct) => Task.FromResult(Audio);
}
