using Hub.Domain.Common;

namespace Hub.Domain.Approvals;

public sealed class Approval : IAggregateRoot
{
    private Approval() { }
    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid RequestedBy { get; private init; }
    public Guid DocumentId { get; private init; }
    public string Action { get; private init; } = "DeleteDocument";
    public string Status { get; private set; } = ApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static Approval Propose(Guid requester, Guid document)
    {
        if (requester == Guid.Empty || document == Guid.Empty) throw new DomainRuleException("A requester and document are required.");
        return new Approval { RequestedBy = requester, DocumentId = document };
    }

    public void Resolve(bool approve)
    {
        if (Status != ApprovalStatus.Pending) throw new DomainRuleException("This approval has already been resolved.", true);
        Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
    }
}
