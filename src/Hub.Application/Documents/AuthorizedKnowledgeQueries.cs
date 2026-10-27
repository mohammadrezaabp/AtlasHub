namespace Hub.Application.Documents;

public static class AuthorizedKnowledgeQueries
{
    public static IQueryable<Document> VisibleTo(this IQueryable<Document> query, HubUser user) => query.Where(DocumentAccessPolicy.For(user));
    public static IQueryable<Chunk> VisibleTo(this IQueryable<Chunk> query, HubUser user) => query.Where(DocumentAccessPolicy.ForChunks(user));
}
