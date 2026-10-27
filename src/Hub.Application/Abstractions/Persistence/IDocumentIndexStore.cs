namespace Hub.Application.Abstractions.Persistence;

public interface IDocumentIndexStore
{
    Task<Document?> ClaimNext(CancellationToken ct);
    Task Complete(Guid id, IReadOnlyCollection<Chunk> chunks, string? embeddingError, CancellationToken ct);
    Task Fail(Guid id, string error, CancellationToken ct);
}
