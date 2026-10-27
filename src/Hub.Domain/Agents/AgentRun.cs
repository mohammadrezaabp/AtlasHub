using Hub.Domain.Common;

namespace Hub.Domain.Agents;

public sealed class AgentRun : IAggregateRoot
{
    private AgentRun() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid UserId { get; private init; }
    public Guid ConversationId { get; private init; }
    public string Prompt { get; private init; } = "";
    public string? ImagePath { get; set; }
    public string Status { get; private set; } = RunStatus.Queued;
    public bool CancelRequested { get; private set; }
    public string? Owner { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static AgentRun Create(Guid user, Guid conversation, string prompt, string? imagePath = null)
    {
        if (user == Guid.Empty || conversation == Guid.Empty || string.IsNullOrWhiteSpace(prompt) || prompt.Length > 6000)
            throw new DomainRuleException("An answer needs a user, conversation, and a question under 6,000 characters.");
        return new AgentRun { UserId = user, ConversationId = conversation, Prompt = prompt, ImagePath = imagePath };
    }

    public void Start(string owner, DateTime leaseUntil)
    {
        if (Status != RunStatus.Queued) throw new DomainRuleException("Only a queued answer can start.", true);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        Status = RunStatus.Running; Owner = owner; LeaseUntil = leaseUntil;
    }

    public void Finish(string outcome)
    {
        if (Status != RunStatus.Running || !RunStatus.IsTerminal(outcome)) throw new DomainRuleException("Invalid answer state transition.", true);
        Status = outcome; LeaseUntil = null;
    }
}
