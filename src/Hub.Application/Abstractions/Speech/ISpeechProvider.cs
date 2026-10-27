namespace Hub.Application.Abstractions.Speech;

public interface ISpeechProvider
{
    Task<Transcription> Transcribe(byte[] bytes, string mime, CancellationToken ct);
    Task<byte[]> Synthesize(string text, CancellationToken ct);
}
