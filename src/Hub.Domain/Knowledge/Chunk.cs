namespace Hub.Domain.Knowledge;

public class Chunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public string Text { get; set; } = "";
    public string Section { get; set; } = "";
    public int Page { get; set; }
    public int Position { get; set; }
    public string EmbeddingModel { get; set; } = "";
    public string? Embedding { get; set; }
}
