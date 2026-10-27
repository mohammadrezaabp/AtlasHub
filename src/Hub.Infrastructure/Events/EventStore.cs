using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Hub.Infrastructure.Events;

public sealed class EventStore(IDbContextFactory<HubDb> factory, IEventMirror mirror) : IEventStore
{
    public async Task Append(Guid run, object value, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = new RunEvent { RunId = run, Data = JsonSerializer.Serialize(value, JsonDefaults.Web) };
        db.Events.Add(item);
        await db.SaveChangesAsync(ct);
        await mirror.Mirror(item);
    }
}
