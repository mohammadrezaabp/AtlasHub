using Hub.Domain.Common;
using Hub.Domain.Identity;
using Hub.Domain.Workspace;

namespace Hub.Domain.Knowledge;

public sealed class Document : IAggregateRoot
{
    private Document() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string FileName { get; private init; } = "";
    public string Path { get; private set; } = "";
    public string ContentType { get; private init; } = "";
    public string Hash { get; private init; } = "";
    public Guid OwnerId { get; private init; }
    public string Visibility { get; private init; } = DocumentVisibility.Organization;
    public string Department { get; private init; } = "General";
    public string Status { get; private set; } = DocumentStatus.Queued;
    public string? Error { get; private set; }
    public int ChunkCount { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public void SetStorageLocation(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        Path = location;
    }

    public static Document Create(HubUser owner, string title, string fileName, string hash, string contentType, string visibility, string team)
    {
        if (visibility is not (DocumentVisibility.Organization or DocumentVisibility.Team or DocumentVisibility.Private)) throw new DomainRuleException("Unknown document access level.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 180) throw new DomainRuleException("Document titles require 1–180 characters.");
        return new Document
        {
            OwnerId = owner.Id,
            Title = title,
            FileName = fileName,
            Hash = hash,
            ContentType = contentType,
            Visibility = visibility,
            Department = new TeamName(team).Value
        };
    }

    public void QueueReindex()
    {
        if (Status == DocumentStatus.Indexing) throw new DomainRuleException("Document indexing is already running.", true);
        Status = DocumentStatus.Queued; Error = null;
    }

    public void BeginIndexing(DateTime now)
    {
        if (Status != DocumentStatus.Queued && !(Status == DocumentStatus.Indexing && LeaseUntil < now))
            throw new DomainRuleException("This document is not available for indexing.", true);
        Status = DocumentStatus.Indexing;
        LeaseUntil = now.AddMinutes(15);
        Attempts++;
    }

    public void CompleteIndexing(int chunks, string? embeddingError)
    {
        if (chunks <= 0) throw new DomainRuleException("An indexed document needs at least one passage.");
        ChunkCount = chunks; Error = embeddingError; Status = embeddingError == null ? DocumentStatus.Ready : DocumentStatus.LexicalOnly; LeaseUntil = null;
    }

    public void EnsureCanDelete()
    {
        if (Status == DocumentStatus.Indexing) throw new DomainRuleException("Wait for indexing to finish before deleting this document.", true);
    }
}
