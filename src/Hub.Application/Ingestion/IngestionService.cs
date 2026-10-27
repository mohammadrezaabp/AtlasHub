using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hub.Application.Ingestion;

public sealed class IngestionService(IDocumentIndexStore documents, IModelClient models, IDocumentTextExtractor extractor,
    IVectorIndex index, IFileStore files, ILogger<IngestionService> logger)
{
    public async Task<bool> ProcessNext(CancellationToken ct)
    {
        var document = await documents.ClaimNext(ct);
        if (document == null) return false;
        try
        {
            using var trace = HubTelemetry.Activity.StartActivity("document.ingest");
            var pieces = await extractor.Extract(document, await files.Read(document.Path, ct), ct);
            if (pieces.Count == 0) throw new InvalidOperationException("No extractable text. Scanned PDFs require OCR; upload page images to use vision.");
            if (pieces.Count > 1500) throw new InvalidOperationException("Document exceeds the local test limit of 1,500 chunks.");
            string? embeddingError = null;
            for (var offset = 0; offset < pieces.Count; offset += 16)
            {
                var batch = pieces.Skip(offset).Take(16).ToArray();
                try
                {
                    var vectors = await models.Embed(batch.Select(c => c.Text).ToArray(), document.OwnerId.ToString(), ct);
                    if (vectors.Length != batch.Length) throw new ModelException("Embedding count does not match input.");
                    for (var i = 0; i < batch.Length; i++)
                    {
                        batch[i].EmbeddingModel = models.Model("Embedding");
                        batch[i].Embedding = JsonSerializer.Serialize(vectors[i]);
                    }
                    try { await index.Index(batch, vectors, models.Model("Embedding"), ct); }
                    catch (Exception e) when (e is not OperationCanceledException)
                    { logger.LogWarning("Qdrant indexing unavailable; persisted vectors remain searchable"); }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    embeddingError = e is ModelException ? e.Message : "Vector indexing is unavailable.";
                    logger.LogWarning("Embedding batch unavailable for document {Document}; lexical search retained", document.Id);
                    break;
                }
            }
            await documents.Complete(document.Id, pieces, embeddingError, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await documents.Fail(document.Id, e is ModelException or InvalidOperationException ? e.Message : "Unable to parse this document. Check its format.", ct);
            logger.LogWarning("Ingestion failed for {Document} with {Type}", document.Id, e.GetType().Name);
        }
        return true;
    }
}
