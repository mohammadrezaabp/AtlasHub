namespace Hub.Application.Documents;

public sealed record DocumentDetails(Guid Id, string Title, string FileName, string Status, IReadOnlyList<PassagePreview> Chunks);
