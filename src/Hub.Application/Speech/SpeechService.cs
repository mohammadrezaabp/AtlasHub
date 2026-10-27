using Microsoft.Extensions.Options;

namespace Hub.Application.Speech;

public sealed class SpeechService(IHubSessionFactory sessions, IFileStore files, ISpeechProvider speech, IOptions<FeatureOptions> features)
{
    public async Task<TranscriptOutput> Transcribe(HubUser user, byte[] bytes, string contentType, Guid? conversation, CancellationToken ct)
    {
        EnsureEnabled();
        if (bytes.Length is <= 0 or > 10_000_000 || !(contentType.StartsWith("audio/") || contentType == "video/webm"))
            throw new UseCaseException(ErrorKind.Validation, "Use an audio recording under 10 MB.");
        await EnsureConversationOwner(user, conversation, ct);
        var mime = contentType.Split(';')[0];
        var extension = mime switch { "audio/mpeg" => ".mp3", "audio/wav" or "audio/x-wav" => ".wav", "audio/ogg" => ".ogg", "audio/mp4" => ".m4a", _ => ".webm" };
        var id = await Save(bytes, mime, extension, "recordings", conversation, user, ct);
        var result = await speech.Transcribe(bytes, mime, ct);
        return new TranscriptOutput(id, result.Transcript, result.Confidence);
    }

    public async Task<SpeechOutput> Synthesize(HubUser user, string text, Guid? conversation, CancellationToken ct)
    {
        EnsureEnabled();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1800) throw new UseCaseException(ErrorKind.Validation, "Speech output is limited to 1,800 characters per request.");
        await EnsureConversationOwner(user, conversation, ct);
        var bytes = await speech.Synthesize(text, ct);
        if (bytes.Length > 20_000_000) throw new UseCaseException(ErrorKind.Validation, "Speech output is too large.");
        var id = await Save(bytes, "audio/mpeg", ".mp3", "speech", conversation, user, ct);
        return new SpeechOutput(id, bytes);
    }

    public async Task<IReadOnlyList<VoiceSummary>> List(HubUser user, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        return await db.ListAsync(db.VoiceAssets.Where(v => v.OwnerId == user.Id || user.Role == UserRoles.Administrator).OrderByDescending(v => v.CreatedAt)
            .Select(v => new VoiceSummary(v.Id, v.Kind, v.Department, v.ConversationId, v.CreatedAt)).Take(100), ct);
    }

    public async Task<FileDownload> Audio(HubUser user, Guid id, CancellationToken ct)
    {
        await using var db = await sessions.CreateAsync(ct);
        var voice = await db.FirstOrDefaultAsync(db.VoiceAssets.Where(v => v.Id == id && (v.OwnerId == user.Id || user.Role == UserRoles.Administrator)), ct)
            ?? throw new UseCaseException(ErrorKind.NotFound, "Audio not found.");
        try { return new FileDownload(await files.Read(voice.Path, ct), voice.ContentType); }
        catch (FileNotFoundException) { throw new UseCaseException(ErrorKind.NotFound, "Audio not found."); }
    }

    private async Task EnsureConversationOwner(HubUser user, Guid? id, CancellationToken ct)
    {
        if (id == null) return;
        await using var db = await sessions.CreateAsync(ct);
        if (!await db.AnyAsync(db.Conversations.Where(c => c.Id == id && c.UserId == user.Id), ct))
            throw new UseCaseException(ErrorKind.NotFound, "Conversation not found.");
    }

    private async Task<Guid> Save(byte[] bytes, string mime, string extension, string kind, Guid? conversation, HubUser user, CancellationToken ct)
    {
        var asset = new VoiceAsset { OwnerId = user.Id, Department = user.Department, ConversationId = conversation, Kind = kind, ContentType = mime };
        asset.Path = await files.Put(StorageKeys.Voice(user, asset.Id, kind, extension), bytes, mime, ct);
        await using var db = await sessions.CreateAsync(ct);
        db.Add(asset);
        await db.SaveChangesAsync(ct);
        return asset.Id;
    }

    private void EnsureEnabled()
    {
        if (!features.Value.Speech) throw new UseCaseException(ErrorKind.Validation, "Speech is disabled.");
    }
}
