namespace Hub.Web.Contracts.Conversations;

public record ChatRequest(string Message, Guid? ConversationId, Guid? ImageDocumentId);
