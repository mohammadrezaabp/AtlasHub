namespace Hub.Application.Approvals;

public sealed record ApprovalSummary(Guid Id, Guid DocumentId, string Action, string Status, DateTime CreatedAt, string? Title);
