namespace Hub.Web.Contracts.Speech;

public record SpeechRequest(string Text, Guid? ConversationId = null);
