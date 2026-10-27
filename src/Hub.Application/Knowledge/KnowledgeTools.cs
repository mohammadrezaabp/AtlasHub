using System.Text.Json.Nodes;

namespace Hub.Application.Knowledge;

public sealed class KnowledgeTools(IHubSessionFactory factory, RetrievalService retrieval)
{
    public static object[] Schemas => [Tool("search_knowledge", "Search authorized organization knowledge and return source passages.", new { query = new { type = "string", maxLength = 1200 } }, ["query"]), Tool("read_source", "Read an authorized source passage by its exact chunk ID.", new { chunkId = new { type = "string", format = "uuid" } }, ["chunkId"]), Tool("workspace_overview", "List documents visible to this user and their indexing state.", new { }, []), Tool("propose_document_deletion", "Request human approval to delete a document owned by this user. This never deletes immediately. Use only on explicit user request and resolve the exact document first.", new { documentId = new { type = "string", format = "uuid" } }, ["documentId"])];

    static object Tool(string name, string description, object properties, string[] required) => new
    {
        name,
        description,
        inputSchema = new
        {
            type = "object",
            properties,
            required,
            additionalProperties = false
        }
    };
    public async Task<object> Execute(string name, JsonObject args, HubUser user, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct);
        switch (name)
        {
            case "search_knowledge":
                var query = args["query"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(query) || query.Length > 1200)
                    throw new ArgumentException("A query of 1-1200 characters is required.");
                return await retrieval.Search(query, user, true, ct);
            case "read_source":
                if (!Guid.TryParse(args["chunkId"]?.ToString(), out var chunkId))
                    throw new ArgumentException("Invalid chunk ID.");
                var c = await db.FirstOrDefaultAsync(db.Chunks.VisibleTo(user).Where(c => c.Id == chunkId), ct);
                return c == null ? new
                {
                    error = "Source unavailable or access denied."
                }

                : new
                {
                    source = new Citation(c.Id, c.DocumentId, c.Document.Title, c.Section, c.Page, c.Text, 1)
                };
            case "workspace_overview":
                return await db.ListAsync(db.Documents.VisibleTo(user).OrderByDescending(d => d.CreatedAt).Select(d => new { d.Id, d.Title, d.Visibility, d.Status, d.ChunkCount }).Take(100), ct);
            case "propose_document_deletion":
                if (!Guid.TryParse(args["documentId"]?.ToString(), out var docId))
                    throw new ArgumentException("Invalid document ID.");
                var document = await db.FirstOrDefaultAsync(db.Documents.VisibleTo(user).Where(d => d.Id == docId), ct);
                if (document == null || (user.Role != UserRoles.Administrator && document.OwnerId != user.Id))
                    throw new UnauthorizedAccessException("Document administration denied.");
                var existing = await db.FirstOrDefaultAsync(db.Approvals.Where(a => a.DocumentId == docId && a.RequestedBy == user.Id && a.Status == ApprovalStatus.Pending), ct);
                if (existing != null)
                    return new
                    {
                        approvalId = existing.Id,
                        status = ApprovalStatus.Pending,
                        document.Title,
                        message = "Waiting for administrator approval."
                    };
                var approval = Approval.Propose(user.Id, docId);
                db.Add<Approval>(approval);
                db.Add<AuditEntry>(new() { Actor = user.Id.ToString(), Action = "DeletionProposed", Detail = docId.ToString() });
                await db.SaveChangesAsync(ct);
                return new
                {
                    approvalId = approval.Id,
                    status = ApprovalStatus.Pending,
                    document.Title,
                    message = "Waiting for administrator approval."
                };
            default:
                throw new ArgumentException("Unknown tool.");
        }
    }
}
