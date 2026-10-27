namespace Hub.Application.Abstractions.AI;

public interface IVectorIndex
{
    Task<List<Guid>?> Search(float[] vector, string model, IReadOnlyList<Chunk> authorizedChunks, CancellationToken ct);
    Task Index(Chunk[] chunks, float[][] vectors, string model, CancellationToken ct);
}
