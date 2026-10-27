using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.Storage;

public sealed class LocalFileStore(IConfiguration config) : IFileStore
{
    private readonly string root = Path.GetFullPath(config["Storage:Path"] ?? "data/uploads");
    public bool IsMinio => false;
    public Task Initialize(CancellationToken ct = default) { Directory.CreateDirectory(root); return Task.CompletedTask; }

    public async Task<string> Put(string key, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        var location = StoragePath.ResolveKey(root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        await File.WriteAllBytesAsync(location, bytes, ct);
        return location;
    }

    public Task<byte[]> Read(string location, CancellationToken ct = default) => File.ReadAllBytesAsync(StoragePath.WithinRoot(root, location), ct);

    public Task Remove(string location, CancellationToken ct = default)
    {
        var path = StoragePath.WithinRoot(root, location);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> Healthy(CancellationToken ct = default) => Task.FromResult(true);
}
