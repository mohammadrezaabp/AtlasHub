namespace Hub.Application.Conversations;

public sealed class ConversationService(IHubSessionFactory sessions, IConversationStore conversations, IStorageMaintenance storage)
{
    public async Task<IReadOnlyList<ConversationSummary>> List(Guid owner, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var prefix = $"chat.pin:{owner}:";
        var values = await db.ListAsync(db.Settings.Where(s => s.Key.StartsWith(prefix)).Select(s => s.Value), ct);
        var pins = values.Where(v => Guid.TryParse(v, out _)).Select(Guid.Parse).ToList();
        return await db.ListAsync(db.Conversations.Where(c => c.UserId == owner)
            .OrderByDescending(c => pins.Contains(c.Id)).ThenByDescending(c => c.CreatedAt)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.ActiveRunId, c.CreatedAt, pins.Contains(c.Id))).Take(100), ct);
    }

    public async Task<IReadOnlyList<Message>> Messages(Guid owner, Guid id, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        if (!await db.AnyAsync(db.Conversations.Where(c => c.Id == id && c.UserId == owner), ct)) throw NotFound();
        return await db.ListAsync(db.Messages.Where(m => m.ConversationId == id).OrderBy(m => m.Id).Take(500), ct);
    }

    public async Task Rename(Guid owner, Guid id, string title, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100) throw new UseCaseException(ErrorKind.Validation, "Use a title of 1–100 characters.");
        if (!await conversations.Rename(id, owner, title, ct)) throw NotFound();
    }

    public async Task SetPinned(Guid owner, Guid id, bool pinned, CancellationToken ct)
    {
        if (!await conversations.SetPinned(id, owner, pinned, ct)) throw NotFound();
    }

    public async Task Delete(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await conversations.Delete(id, owner, ct)) throw NotFound();
        await storage.DrainDeletes(ct);
    }

    public async Task<RunStarted> Start(HubUser user, string message, Guid? conversationId, Guid? imageDocumentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 6000) throw new UseCaseException(ErrorKind.Validation, "Enter a message under 6,000 characters.");
        await using var db = await sessions.CreateAsync(ct);
        var conversation = conversationId == null ? Conversation.Create(user.Id, message[..Math.Min(65, message.Length)]) :
            await db.FirstOrDefaultAsync(db.Conversations.Where(c => c.Id == conversationId && c.UserId == user.Id), ct);
        if (conversation == null) throw NotFound();
        string? imagePath = null;
        if (imageDocumentId.HasValue)
        {
            var image = await db.FirstOrDefaultAsync(db.Documents.VisibleTo(user).Where(d => d.Id == imageDocumentId), ct);
            if (image == null || !DocumentFormats.IsImage(image.FileName)) throw new UseCaseException(ErrorKind.Validation, "Image unavailable.");
            imagePath = image.Path;
        }
        var run = AgentRun.Create(user.Id, conversation.Id, message, imagePath);
        if (!await conversations.StartRun(conversation, run, conversationId == null, ct))
            throw new UseCaseException(ErrorKind.Conflict, "This conversation already has an active answer.");
        return new RunStarted(run.Id, conversation.Id);
    }

    public async Task Stop(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await conversations.RequestStop(id, owner, ct)) throw NotFound();
    }

    private static UseCaseException NotFound() => new(ErrorKind.NotFound, "Conversation or answer not found.");
}
