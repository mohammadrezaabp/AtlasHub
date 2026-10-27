namespace Hub.Application.Documents;

public sealed record DocumentUpload(string FileName, string ContentType, byte[] Bytes, string Title, string Visibility, string? Team);
