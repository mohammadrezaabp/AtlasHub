using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Persistence;

public sealed class EfHubSessionFactory(IDbContextFactory<HubDb> factory) : IHubSessionFactory
{
    public async Task<IHubSession> CreateAsync(CancellationToken ct = default) => new EfHubSession(await factory.CreateDbContextAsync(ct));
}
