namespace Hub.Application.Abstractions.AI;

public interface IDocumentTextExtractor
{
    Task<List<Chunk>> Extract(Document document, byte[] bytes, CancellationToken ct);
}
