namespace Hub.Domain.Conversations;

public class VoiceAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? ConversationId { get; set; }
    public string Department { get; set; } = "General";
    public string Kind { get; set; } = "recordings";
    public string Path { get; set; } = "";
    public string ContentType { get; set; } = "audio/webm";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
