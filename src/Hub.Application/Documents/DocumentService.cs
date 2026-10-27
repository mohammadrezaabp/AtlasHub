using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Hub.Application.Documents;

public sealed class DocumentService(IHubSessionFactory sessions, IFileStore files, IOptions<FeatureOptions> features)
{
    public async Task<IReadOnlyList<DocumentSummary>> List(HubUser user, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        return await db.ListAsync(db.Documents.VisibleTo(user).OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id).Select(d =>
            new DocumentSummary(d.Id, d.Title, d.FileName, d.Status, d.Error, d.Visibility, d.Department, d.ChunkCount, d.CreatedAt, user.Role == UserRoles.Administrator || d.OwnerId == user.Id)), ct);
    }

    public async Task<DocumentDetails> Details(HubUser user, Guid id, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var document = await db.FirstOrDefaultAsync(db.Documents.VisibleTo(user).Where(d => d.Id == id), ct) ?? throw NotFound();
        var chunks = await db.ListAsync(db.Chunks.Where(c => c.DocumentId == id).OrderBy(c => c.Position)
            .Select(c => new PassagePreview(c.Id, c.Text, c.Section, c.Page)).Take(500), ct);
        return new DocumentDetails(document.Id, document.Title, document.FileName, document.Status, chunks);
    }

    public async Task<FileDownload> Download(HubUser user, Guid id, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var document = await db.FirstOrDefaultAsync(db.Documents.VisibleTo(user).Where(d => d.Id == id), ct) ?? throw NotFound();
        try { return new FileDownload(await files.Read(document.Path, ct), "application/octet-stream", document.FileName); }
        catch (FileNotFoundException) { throw NotFound(); }
    }

    public async Task Reindex(HubUser user, Guid id, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var document = await db.FirstOrDefaultAsync(db.Documents.Where(d => d.Id == id && (d.OwnerId == user.Id || user.Role == UserRoles.Administrator)), ct) ?? throw NotFound();
        document.QueueReindex();
        await db.SaveChangesAsync(ct);
    }

    public async Task<UploadedDocument> Upload(HubUser user, DocumentUpload upload, CancellationToken ct)
    {
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (upload.Bytes.Length is <= 0 or > 20_000_000 || !DocumentFormats.Extensions.Contains(extension))
            throw new UseCaseException(ErrorKind.Validation, "Upload a PDF, Markdown, text, CSV or image up to 20 MB.");
        if (DocumentFormats.IsImage(upload.FileName) && !features.Value.Vision) throw new UseCaseException(ErrorKind.Validation, "Vision is disabled.");
        if (!DocumentFormats.Matches(extension, upload.Bytes)) throw new UseCaseException(ErrorKind.Validation, "File contents do not match the selected format.");
        var visibility = upload.Visibility is DocumentVisibility.Organization or DocumentVisibility.Team or DocumentVisibility.Private ? upload.Visibility : DocumentVisibility.Private;
        var team = user.Role == UserRoles.Administrator && !string.IsNullOrWhiteSpace(upload.Team) ? upload.Team.Trim() : user.Department;
        var hash = Convert.ToHexString(SHA256.HashData(upload.Bytes));
        await using var db = await sessions.CreateAsync(ct);
        var duplicate = await db.FirstOrDefaultAsync(db.Documents.Where(d => d.Hash == hash && d.OwnerId == user.Id && d.Visibility == visibility && d.Department == team), ct);
        if (duplicate != null) return new UploadedDocument(duplicate.Id, duplicate.Title, duplicate.Status, true);
        var title = upload.Title.Trim();
        if (title.Length == 0) title = Path.GetFileNameWithoutExtension(upload.FileName);
        var document = Document.Create(user, title[..Math.Min(180, title.Length)], Path.GetFileName(upload.FileName), hash, upload.ContentType, visibility, team);
        document.SetStorageLocation(await files.Put(StorageKeys.Document(document), upload.Bytes, string.IsNullOrEmpty(document.ContentType) ? "application/octet-stream" : document.ContentType, ct));
        db.Add(document);
        db.Add(new AuditEntry { Actor = user.Id.ToString(), Action = "DocumentUploaded", Detail = document.Id.ToString() });
        await db.SaveChangesAsync(ct);
        return new UploadedDocument(document.Id, document.Title, document.Status);
    }

    private static UseCaseException NotFound() => new(ErrorKind.NotFound, "Document not found.");
}
