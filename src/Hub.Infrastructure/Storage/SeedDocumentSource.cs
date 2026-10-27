using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.Storage;

public sealed class SeedDocumentSource(IConfiguration config) : ISeedDocumentSource
{
    public async IAsyncEnumerable<SeedDocument> Read([EnumeratorCancellation] CancellationToken ct)
    {
        var path = config["Seed:Path"] ?? "/seed";
        if (!Directory.Exists(path)) yield break;
        foreach (var file in Directory.EnumerateFiles(path).Where(f => DocumentFormats.Extensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            yield return new SeedDocument(Path.GetFileName(file), await File.ReadAllBytesAsync(file, ct));
    }
}
