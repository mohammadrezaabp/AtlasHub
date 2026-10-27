using Hub.Domain.Common;

namespace Hub.Domain.Conversations;

public sealed class Conversation : IAggregateRoot
{
    private Conversation() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid UserId { get; private init; }
    public string Title { get; private set; } = "New conversation";
    public Guid? ActiveRunId { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static Conversation Create(Guid user, string title)
    {
        var conversation = new Conversation { UserId = user };
        conversation.Rename(title);
        return conversation;
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100) throw new DomainRuleException("Use a title of 1–100 characters.");
        Title = title.Trim();
    }

    public void BeginRun(Guid id)
    {
        if (ActiveRunId != null) throw new DomainRuleException("This conversation already has an active answer.", true);
        ActiveRunId = id;
    }

    public void EnsureCanDelete()
    {
        if (ActiveRunId != null) throw new DomainRuleException("Stop the active answer before deleting this chat.", true);
    }
}
