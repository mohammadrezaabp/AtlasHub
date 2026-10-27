namespace Hub.Application.Documents;

public sealed record UploadedDocument(Guid Id, string Title, string Status, bool Duplicate = false);
