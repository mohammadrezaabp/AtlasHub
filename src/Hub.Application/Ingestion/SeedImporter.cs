using System.Security.Cryptography;

namespace Hub.Application.Ingestion;

public sealed class SeedImporter(IHubSessionFactory factory, IFileStore files, ISeedDocumentSource source)
{
    public async Task Import(CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct);
        var admin = await db.FirstOrDefaultAsync(db.Users.Where(u => u.Role == UserRoles.Administrator), ct);
        if (admin == null)
            return;
        var imported = new HashSet<string>();
        await foreach (var file in source.Read(ct))
        {
            var bytes = file.Bytes;
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!imported.Add(hash) || await db.AnyAsync(db.Documents.Where(d => d.Hash == hash && d.OwnerId == admin.Id), ct))
                continue;
            var document = Document.Create(admin, Path.GetFileNameWithoutExtension(file.FileName), file.FileName, hash, "application/octet-stream", DocumentVisibility.Organization, admin.Department);
            document.SetStorageLocation(await files.Put(StorageKeys.Document(document), bytes, "application/octet-stream", ct));
            db.Add<Document>(document);
        }

        await db.SaveChangesAsync(ct);
    }
}
