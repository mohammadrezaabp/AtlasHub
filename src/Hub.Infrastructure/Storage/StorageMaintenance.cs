using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace Hub.Infrastructure.Storage;

public sealed class StorageMaintenance(IDbContextFactory<HubDb> factory, IFileStore files, IConfiguration config, ILogger<StorageMaintenance> logger) : IStorageMaintenance
{
    public async Task Migrate(CancellationToken ct = default)
    {
        await files.Initialize(ct);
        if (!files.IsMinio) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var documents = await db.Documents.Where(d => !d.Path.StartsWith("s3://")).ToListAsync(ct);
        var root = Path.GetFullPath(config["Storage:Path"] ?? "data/uploads") + Path.DirectorySeparatorChar;
        foreach (var d in documents)
        {
            var previous = Path.GetFullPath(d.Path);
            if (!previous.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("Legacy document is outside the upload directory.");
            var bytes = await File.ReadAllBytesAsync(previous, ct);
            var location = await files.Put(StorageKeys.Document(d), bytes, string.IsNullOrEmpty(d.ContentType) ? "application/octet-stream" : d.ContentType, ct);
            var check = await files.Read(location, ct);
            if (!SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(check))) throw new IOException("Object migration verification failed.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Runs.Where(r => r.ImagePath == d.Path).ExecuteUpdateAsync(s => s.SetProperty(r => r.ImagePath, location), ct);
            d.SetStorageLocation(location); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            // Keep the old upload volume as a recoverable migration backup.
            logger.LogInformation("Migrated document {Document} to object storage", d.Id);
        }
    }
    public async Task DrainDeletes(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var entry in await db.Settings.Where(s => s.Key.StartsWith("storage.delete:")).OrderBy(s => s.Key).Take(20).ToListAsync(ct))
        {
            try { await files.Remove(entry.Value, ct); db.Settings.Remove(entry); await db.SaveChangesAsync(ct); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogWarning("Storage cleanup will retry after {Type}", e.GetType().Name); }
        }
    }
}
