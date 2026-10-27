namespace Hub.Application.Speech;

public sealed record SpeechOutput(Guid AudioId, byte[] Bytes);
