namespace Hub.Application.Documents;

public sealed record DocumentSummary(Guid Id, string Title, string FileName, string Status, string? Error, string Visibility, string Department, int ChunkCount, DateTime CreatedAt, bool CanManage);
