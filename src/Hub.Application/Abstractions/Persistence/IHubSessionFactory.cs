namespace Hub.Application.Abstractions.Persistence;

public interface IHubSessionFactory
{
    Task<IHubSession> CreateAsync(CancellationToken ct = default);
}
