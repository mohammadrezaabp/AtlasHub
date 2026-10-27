namespace Hub.Application.Search;

public record Citation(Guid ChunkId, Guid DocumentId, string Title, string Section, int Page, string Excerpt, double Score);
