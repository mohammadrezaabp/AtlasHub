namespace Hub.Application.Agents;

public sealed class RunEventReader(IHubSessionFactory sessions)
{
    public async Task EnsureOwner(Guid id, Guid user, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        if (!await db.AnyAsync(db.Runs.Where(r => r.Id == id && r.UserId == user), ct))
            throw new UseCaseException(ErrorKind.NotFound, "Answer not found.");
    }

    public async Task<RunEventPage> Read(Guid id, long after, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var events = await db.ListAsync(db.Events.Where(e => e.RunId == id && e.Id > after).OrderBy(e => e.Id).Take(300), ct);
        var finished = events.Count < 300 && await db.AnyAsync(db.Runs.Where(r => r.Id == id && r.Status != RunStatus.Queued && r.Status != RunStatus.Running), ct);
        return new RunEventPage(events, finished);
    }
}
