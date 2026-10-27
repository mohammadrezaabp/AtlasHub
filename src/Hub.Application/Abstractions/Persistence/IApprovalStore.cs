namespace Hub.Application.Abstractions.Persistence;

public interface IApprovalStore
{
    Task<bool> Resolve(Guid id, HubUser administrator, bool approve, CancellationToken ct);
}
