namespace Hub.Application.Speech;

public sealed record TranscriptOutput(Guid AudioId, string Transcript, double Confidence);
