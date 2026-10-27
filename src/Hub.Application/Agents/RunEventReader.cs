namespace Hub.Application.Agents;

public sealed class RunEventReader(IHubSessionFactory sessions, IdentityService identity)
{
    public async Task<RunEventPage> Read(Guid id, long after, Guid owner, string sessionVersion, CancellationToken ct)
    {
        await identity.GetSessionUser(owner, sessionVersion, ct);
        await using var db = await sessions.CreateAsync(ct);
        var status = await db.FirstOrDefaultAsync(db.Runs.Where(run => run.Id == id && run.UserId == owner).Select(run => run.Status), ct)
            ?? throw new UseCaseException(ErrorKind.NotFound, "Answer not found.");
        var events = await db.ListAsync(db.Events.Where(e => e.RunId == id && e.Id > after).OrderBy(e => e.Id).Take(300), ct);
        var finished = events.Count < 300 && RunStatus.IsTerminal(status);
        return new RunEventPage(events, finished);
    }
}
