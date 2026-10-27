namespace Hub.Application.Abstractions.Storage;

public interface IStorageMaintenance
{
    Task Migrate(CancellationToken ct = default);
    Task DrainDeletes(CancellationToken ct = default);
}
