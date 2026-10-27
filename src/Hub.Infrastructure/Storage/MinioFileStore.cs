using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Hub.Infrastructure.Storage;

public sealed class MinioFileStore : IFileStore, IDisposable
{
    private readonly IMinioClient client;
    private readonly LocalFileStore legacyFiles;
    private readonly string bucket;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;
    public bool IsMinio => true;

    public MinioFileStore(IConfiguration config, LocalFileStore legacyFiles)
    {
        this.legacyFiles = legacyFiles;
        bucket = config["Storage:Bucket"] ?? "atlas-hub";
        var endpoint = new Uri(config["Storage:Endpoint"] ?? "http://minio:9000");
        client = new MinioClient().WithEndpoint(endpoint.Host, endpoint.Port)
            .WithCredentials(config["Storage:AccessKey"]!, config["Storage:SecretKey"]!)
            .WithSSL(endpoint.Scheme == "https").Build();
    }

    public async Task Initialize(CancellationToken ct = default)
    {
        if (initialized) return;
        await gate.WaitAsync(ct);
        try
        {
            if (initialized) return;
            if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct))
            {
                try { await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct); }
                catch (MinioException) { if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)) throw; }
            }
            initialized = true;
        }
        finally { gate.Release(); }
    }

    public async Task<string> Put(string key, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        _ = StoragePath.ResolveKey(Path.GetTempPath(), key);
        await Initialize(ct);
        using var stream = new MemoryStream(bytes, false);
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(key).WithStreamData(stream)
            .WithObjectSize(bytes.Length).WithContentType(contentType), ct);
        return $"s3://{bucket}/{key}";
    }

    public async Task<byte[]> Read(string location, CancellationToken ct = default)
    {
        if (!location.StartsWith("s3://", StringComparison.Ordinal)) return await legacyFiles.Read(location, ct);
        using var result = new MemoryStream();
        try
        {
            await client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket).WithObject(ObjectKey(location))
                .WithCallbackStream((stream, token) => stream.CopyToAsync(result, token)), ct);
        }
        catch (ObjectNotFoundException e) { throw new FileNotFoundException("Stored file was not found.", e); }
        return result.ToArray();
    }

    public async Task Remove(string location, CancellationToken ct = default)
    {
        if (!location.StartsWith("s3://", StringComparison.Ordinal)) { await legacyFiles.Remove(location, ct); return; }
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(ObjectKey(location)), ct);
    }

    public Task<bool> Healthy(CancellationToken ct = default) => client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);

    private string ObjectKey(string location)
    {
        var prefix = $"s3://{bucket}/";
        if (!location.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected storage location.");
        var key = location[prefix.Length..];
        _ = StoragePath.ResolveKey(Path.GetTempPath(), key);
        return key;
    }

    public void Dispose() { client.Dispose(); gate.Dispose(); }
}
