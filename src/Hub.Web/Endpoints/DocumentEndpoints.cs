using System.Text.Json.Nodes;
using Hub.Web.Security;

namespace Hub.Web.Endpoints;

public static class DocumentEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/documents", async (CurrentUser user, DocumentService documents, CancellationToken ct) => Results.Ok(await documents.List(await user.Get(ct), ct)));
        api.MapPost("/documents", Upload).DisableAntiforgery();
        api.MapGet("/documents/{id:guid}", async (Guid id, CurrentUser user, DocumentService documents, CancellationToken ct) => Results.Ok(await documents.Details(await user.Get(ct), id, ct)));
        api.MapGet("/documents/{id:guid}/download", async (Guid id, CurrentUser user, DocumentService documents, CancellationToken ct) =>
        {
            var file = await documents.Download(await user.Get(ct), id, ct);
            return Results.File(file.Bytes, file.ContentType, file.FileName);
        });
        api.MapPost("/documents/{id:guid}/reindex", async (Guid id, CurrentUser user, DocumentService documents, CancellationToken ct) =>
        {
            await documents.Reindex(await user.Get(ct), id, ct);
            return Results.Ok();
        });
        api.MapPost("/documents/{id:guid}/delete-request", async (Guid id, CurrentUser user, KnowledgeTools tools, CancellationToken ct) =>
            Results.Ok(await tools.Execute("propose_document_deletion", new JsonObject { ["documentId"] = id.ToString() }, await user.Get(ct), ct)));
    }

    private static async Task<IResult> Upload(HttpContext context, CurrentUser user, DocumentService documents)
    {
        var ct = context.RequestAborted;
        var form = await context.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file == null || file.Length is <= 0 or > 20_000_000) throw new UseCaseException(ErrorKind.Validation, "Upload a file up to 20 MB.");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        var request = new DocumentUpload(file.FileName, file.ContentType, memory.ToArray(), form["title"].ToString(), form["visibility"].ToString(), form["department"].ToString());
        return Results.Ok(await documents.Upload(await user.Get(ct), request, ct));
    }
}
