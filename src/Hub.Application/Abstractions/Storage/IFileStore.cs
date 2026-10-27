namespace Hub.Application.Abstractions.Storage;

public interface IFileStore
{
    bool IsMinio { get; }
    Task Initialize(CancellationToken ct = default);
    Task<string> Put(string key, byte[] bytes, string contentType, CancellationToken ct = default);
    Task<byte[]> Read(string location, CancellationToken ct = default);
    Task Remove(string location, CancellationToken ct = default);
    Task<bool> Healthy(CancellationToken ct = default);
}
