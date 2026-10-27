namespace Hub.Application.Conversations;

public sealed record ConversationSummary(Guid Id, string Title, Guid? ActiveRunId, DateTime CreatedAt, bool Pinned);
