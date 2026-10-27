namespace Hub.Application.Speech;

public sealed record VoiceSummary(Guid Id, string Kind, string Department, Guid? ConversationId, DateTime CreatedAt);
