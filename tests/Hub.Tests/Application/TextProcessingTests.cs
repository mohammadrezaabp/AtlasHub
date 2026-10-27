using Xunit;

namespace Hub.Tests.Application;

public sealed class TextProcessingTests
{
    [Fact]
    public void Embedding_spaces_and_chunking_preserve_invariants()
    {
        Assert.NotEqual(EmbeddingSpace.CollectionName("model-a"), EmbeddingSpace.CollectionName("model-b"));
        Assert.Equal(0, VectorMath.Cosine([1, 2], [1]));
        Assert.Equal(1, VectorMath.Cosine([1, 0], [1, 0]), 6);
        var chunks = TextChunker.Split(Guid.NewGuid(), "# Exact\nRevenue is 18.75 percent.\n" + string.Join(' ', Enumerable.Repeat("Knowledge", 900)), 0);
        Assert.True(chunks.Count > 2);
        Assert.Contains("18.75", chunks[0].Text);
        Assert.All(chunks, c => Assert.Equal("Exact", c.Section));
        Assert.Equal("یک کتاب", TextChunker.Normalize("يك کتاب"));
        Assert.Equal("PostgreSQL-safe text", TextChunker.Normalize("PostgreSQL-safe\0 text"));
    }
}
