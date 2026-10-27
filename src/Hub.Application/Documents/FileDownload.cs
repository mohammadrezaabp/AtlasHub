namespace Hub.Application.Documents;

public sealed record FileDownload(byte[] Bytes, string ContentType, string? FileName = null);
