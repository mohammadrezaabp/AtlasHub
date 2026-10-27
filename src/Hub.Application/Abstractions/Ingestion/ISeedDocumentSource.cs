namespace Hub.Application.Abstractions.Ingestion;

public interface ISeedDocumentSource
{
    IAsyncEnumerable<SeedDocument> Read(CancellationToken ct);
}
